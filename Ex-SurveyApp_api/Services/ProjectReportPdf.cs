using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SkiaSharp;

namespace Ex_SurveyApp_api.Services;

// One fixed report template. Stored survey data supplies every project-specific value.
public sealed class ProjectReportPdf(IProjectPhotoStorage storage, ILogger logger)
{
    private static readonly object FontLock = new();
    private readonly Dictionary<string, byte[]?> assetBytes = new();
    public async Task<byte[]> Generate(ProjectReport report, CancellationToken ct)
    {
        EnsureFonts();
        foreach (var asset in report.Assets) {
            ct.ThrowIfCancellationRequested();
            try {
                var file = await storage.OpenReadAsync(asset.StorageKey, ct);
                if (file is null) {assetBytes[asset.Key] = null; continue;}
                await using var source = file.Stream;
                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer, ct);
                assetBytes[asset.Key] = buffer.ToArray();
            }
            catch (OperationCanceledException) {throw;}
            catch (Exception ex) {logger.LogWarning(ex, "Report asset unavailable {AssetKey}", asset.Key); assetBytes[asset.Key] = null;}
        }
        using var writer = new Writer(report, assetBytes, logger, ct);
        return writer.Build();
    }

    public static void EnsureFonts() { lock (FontLock) {GlobalFontSettings.FontResolver ??= new ReportFonts();} }

    public static string Answer(string? value) => value?.Trim().ToLowerInvariant() switch {
        "true" or "yes" or "pass" or "p" => "PASS",
        "false" or "no" or "fail" or "f" => "FAIL",
        "n/a" or "na" or "not applicable" => "N/A",
        null or "" => "-", _ => Clean(value)
    };
    private static string Clean(string? text) => string.IsNullOrWhiteSpace(text) || text.Trim().ToLowerInvariant() is "null" or "undefined" or "nan" ? "-" : text.Trim().Replace('\r', ' ');
    private static string Date(DateTimeOffset? value) => value?.ToString("dd.MM.yyyy") ?? "-";
    private static string EquipmentId(ReportEquipment e) => string.IsNullOrWhiteSpace(e.EquipmentId) ? Clean(e.TagNumber) : e.EquipmentId;
    private static string Status(string? value) => value switch {
        "QcApproved" => "QC Approved", "PendingReview" => "Pending Review", "InReview" => "In Review",
        "ChangesRequested" => "Changes Requested", "NotSubmitted" => "Not Submitted",
        "RectificationSubmitted" => "Rectification Submitted", _ => Clean(value)
    };

    private sealed class Writer(ProjectReport report, Dictionary<string, byte[]?> bytes, ILogger logger, CancellationToken ct) : IDisposable
    {
        private readonly PdfDocument pdf = new();
        private readonly List<(string Label, int Page)> contents = new();
        private readonly List<PdfPage> toc = new();
        private PdfPage page = null!;
        private XGraphics gfx = null!;
        private double y;
        private bool wide;
        private const double Margin = 35;
        private double Width => page.Width.Point;
        private double Height => page.Height.Point;
        private double BodyWidth => Width - Margin * 2;
        private static readonly XPen Grid = new(XColors.Black, .45);
        private static readonly XBrush Blue = new XSolidBrush(XColor.FromArgb(156, 194, 229));
        private static XFont Font(double size = 10, bool bold = false, bool serif = false, bool italic = false) => new(serif ? "Report Serif" : "Report Sans", size,
            bold ? XFontStyleEx.Bold : italic ? XFontStyleEx.Italic : XFontStyleEx.Regular);
        private sealed record Cell(string? Text, ReportAsset? Image = null);
        private static Cell[] Cells(params string?[] text) => text.Select(t => new Cell(t)).ToArray();

        public byte[] Build()
        {
            pdf.Info.Title = "Explosion Proof Survey Report - " + report.ProjectName;
            pdf.Info.Author = report.PreparedBy;
            pdf.Info.Creator = "Ex-Survey";
            var cover = Blank();
            NewPage();
            RevisionHistory();
            var expectedContents = new[] {"1. Introduction", "1.1 Executive Summary", "1.2 Applicable Standards and References", "2. HAZARDOUS AREA PLAN", "3. HAZARDOUS AREA EQUIPMENT REGISTER & EQUIPMENT LAYOUT"}
                .Concat(report.Areas.Select((a, n) => $"3.{n + 1} {a.Name}"))
                .Concat(new[] {"4. EQUIPMENT DEFECT & RECOMMENDATION REGISTER", "5. EQUIPMENT CHECK LIST (IEC 60079-17)"})
                .Concat(report.SharedLayouts.Select((layout, n) => $"3.{report.Areas.Count + n + 1} {layout.Name}"))
                .Concat(report.Areas.Select((a, n) => $"5.{n + 1} {a.Name}"));
            var tocY = 110.5;
            toc.Add(Blank());
            foreach (var label in expectedContents) {
                var height = Wrap(label, Font(10, serif: true), BodyWidth - 45).Count * 15 + 6;
                if (tocY + height > Height - 60) {toc.Add(Blank()); tocY = 110.5;}
                tocY += height;
            }
            NewPage(); Mark("1. Introduction");
            var inspections = report.Areas.SelectMany(a => a.Equipment).SelectMany(e => e.Inspections).ToArray();
            Introduction();
            Divider("2. HAZARDOUS AREA PLAN");
            if (report.HazardousAreaPlan is null) {NewPage(); Heading("HAZARDOUS AREA PLAN"); Paragraph("No Hazardous Area Plan Uploaded");}
            else Plan(report.HazardousAreaPlan);
            Divider("3. HAZARDOUS AREA EQUIPMENT REGISTER & EQUIPMENT LAYOUT");
            if (report.Areas.Count == 0) {NewPage(); Paragraph("No project areas available");}
            for (var a = 0; a < report.Areas.Count; a++) {
                ct.ThrowIfCancellationRequested();
                var area = report.Areas[a];
                Divider($"3.{a + 1} {area.Name}");
                if (area.Layout is null && area.Equipment.Count == 0) {NewPage(); Heading(area.Name); Paragraph("No area layout or equipment records available"); continue;}
                IReadOnlyList<ReportAreaLayout> areaLayouts = area.Layouts.Count > 0 ? area.Layouts : area.Layout is null
                    ? Array.Empty<ReportAreaLayout>() : new[] {new ReportAreaLayout("legacy", "Area Layout", area.Layout)};
                if (areaLayouts.Count == 0) {NewPage(true); Heading($"3.{a + 1} {area.Name} - EQUIPMENT LAYOUT"); Paragraph("No Area Layout Uploaded");}
                foreach (var layout in areaLayouts) {
                    NewPage(true); Heading($"3.{a + 1} {area.Name} - {Clean(layout.Name).ToUpperInvariant()}");
                    var pins = area.Equipment.Where(e => e.LayoutImageKey == layout.Key || e.LayoutImageKey is null && area.Layout?.Key == layout.Image.Key).ToArray();
                    DrawImage(layout.Image, new XRect(Margin, y + 10, BodyWidth, Height - y - 80), pins);
                }
                if (area.Equipment.Count == 0) continue;
                EquipmentIndex(area);
                NewPage(true); EquipmentRegisterHeading();
                Table(["Sl. No", "Equipment ID", "Tag Number", "Area", "Type", "Location", "Manufacturer", "Circuit ID", "Protection Details", "Certificate Number", "Inspection Type", "Inspection Date", "Inspector", "Comments", "QC Status"],
                    [.55, 1, 1, .9, 1.2, 1, 1.2, .9, 1.3, 1.3, .85, .9, 1.1, 1.8, 1],
                    area.Equipment.SelectMany(e => e.Inspections.Count == 0 ? new[] {Register(e, area, null)} : e.Inspections.Select(i => Register(e, area, i))), 7.5, "HAZARDOUS AREA EQUIPMENT REGISTER", true,
                    continuationHeading: EquipmentRegisterHeading, centered: true);
            }
            for (var n = 0; n < report.SharedLayouts.Count; n++) {
                var layout = report.SharedLayouts[n];
                Divider($"3.{report.Areas.Count + n + 1} PROJECT-WIDE LAYOUT");
                NewPage(true); Heading($"3.{report.Areas.Count + n + 1} {Clean(layout.Name).ToUpperInvariant()}");
                DrawImage(layout.Image, new XRect(Margin, y + 10, BodyWidth, Height - y - 80), layout.Equipment);
            }
            Divider("4. EQUIPMENT DEFECT & RECOMMENDATION REGISTER");
            NewPage(true); DefectRegisterHeading();
            var defects = new List<Cell[]>(); var action = 0;
            foreach (var area in report.Areas) foreach (var e in area.Equipment) foreach (var i in e.Inspections) {
                foreach (var f in i.Findings) {
                    defects.Add(Defect(++action, area, e, i, f.Observation, f.Recommendation, f.Status, f.Severity, f.Photos));
                }
                // Failed saved answers remain visible even when no standalone Finding was recorded.
                if (i.Findings.Count == 0) foreach (var answer in i.Answers.Where(a => Answer(a.Value) == "FAIL"))
                    defects.Add(Defect(++action, area, e, i, answer.Question + (string.IsNullOrWhiteSpace(answer.Comment) ? "" : "\n" + answer.Comment), null, "Checklist FAIL", null, i.Photos));
            }
            Table(["Action No", "Date", "Equipment ID", "Tag Number", "Area", "Inspection Type", "Description", "Observation", "Recommendation", "Severity / Status", "Photo", "Photo", "Remarks"],
                [.6, .9, 1, 1, .9, .85, 1.2, 2, 2, 1, 1.8, 1.8, 1.7], defects, 7.5, "EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER", true, new XSolidBrush(XColor.FromArgb(215, 222, 230)), continuationHeading: DefectRegisterHeading);
            Divider("5. EQUIPMENT CHECK LIST (IEC 60079-17)");
            if (!inspections.Any()) {NewPage(); Paragraph("No inspection records available");}
            for (var a = 0; a < report.Areas.Count; a++) {
                var area = report.Areas[a];
                Divider($"5.{a + 1} {area.Name}");
                if (!area.Equipment.Any(e => e.Inspections.Count > 0)) {NewPage(); Paragraph("No inspection records available for this area");}
                foreach (var e in area.Equipment) foreach (var i in e.Inspections) {
                    ct.ThrowIfCancellationRequested(); NewPage();
                    Heading("EX Inspection Test Record", 13);
                    Table(["Project / Item", "Equipment / Location", "Inspection Details"], [1, 1, 1], [
                        Cells("Project: " + report.ProjectName, "Equipment ID: " + EquipmentId(e), "Date: " + Date(i.Date)),
                        Cells("Item Tag: " + e.TagNumber, "Location: " + Clean(e.Location), "Type: " + e.Type),
                        Cells("Area: " + area.Name, "Manufacturer: " + Clean(e.Manufacturer), "Inspector: " + i.Inspector),
                        Cells("Circuit ID: " + Clean(e.CircuitId), "Certificate: " + Clean(e.CertificateNumber), "Grade: " + Clean(i.InspectionType)),
                        Cells("Protection: " + Clean(e.ProtectionDetails), "QC: " + Status(i.Status), "Checked By: " + i.CheckedBy)
                    ], 9, "EX Inspection Test Record - " + e.TagNumber, false, XBrushes.White);
                    y += 12;
                    Table(["No", "Items to be checked", "D", "C", "V", "Remarks"], [.6, 6, .8, .8, .8, 2.4],
                        i.Answers.Select((answer, index) => Cells((index + 1).ToString(), answer.Question,
                            (answer.InspectionType ?? i.InspectionType) == "Detailed" ? Answer(answer.Value) : "-",
                            (answer.InspectionType ?? i.InspectionType) == "Close" ? Answer(answer.Value) : "-",
                            (answer.InspectionType ?? i.InspectionType) == "Visual" ? Answer(answer.Value) : "-",
                            Clean(answer.Comment) + ((answer.InspectionType ?? i.InspectionType) is not ("Detailed" or "Close" or "Visual") ? "\nResult: " + Answer(answer.Value) : ""))),
                        9, "EQUIPMENT CHECK LIST - " + e.TagNumber, false, XBrushes.White);
                    y += 12;
                    Paragraph("Surveyor Comments: " + Clean(i.Comments));
                    Paragraph("QC Result: " + Status(i.Status) + "\nQC Comments: " + Clean(i.QcComments) + "\nQC Severity: " + Clean(i.QcSeverity) + "\nReviewed By: " + i.CheckedBy + "\nReviewed At: " + Date(i.ReviewedAt), "QC REVIEW DETAILS - " + e.TagNumber);
                }
            }
            gfx?.Dispose(); gfx = null!;
            Cover(cover); Contents(); Headers();
            using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
        }

        private Cell[] Register(ReportEquipment e, ReportArea area, ReportInspection? i) => Cells(e.SequenceNo.ToString(), EquipmentId(e) + (string.IsNullOrWhiteSpace(e.RfidTagUid) ? "" : "\nRFID UID: " + Clean(e.RfidTagUid)), e.TagNumber, area.Name, e.Type, e.Location,
            e.Manufacturer, e.CircuitId, e.ProtectionDetails, e.CertificateNumber, i?.InspectionType, Date(i?.Date), i?.Inspector, i?.Comments, i is null ? "Not inspected" : Status(i.Status));
        private Cell[] Defect(int action, ReportArea a, ReportEquipment e, ReportInspection i, string observation, string? recommendation, string status, string? severity, IReadOnlyList<ReportAsset> photos) => [
            ..Cells(action.ToString(), Date(i.Date), EquipmentId(e), e.TagNumber, a.Name, i.InspectionType, e.Type, observation, recommendation, Clean(severity) + "\n" + Status(status)),
            new(null, photos.ElementAtOrDefault(0)), new(null, photos.ElementAtOrDefault(1)), new(i.Comments)
        ];
        private PdfPage Blank(bool landscape = false) {var p = pdf.AddPage(); p.Size = landscape ? PageSize.A3 : PageSize.A4; p.Orientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait; return p;}
        private void NewPage(bool landscape = false, string? title = null) {gfx?.Dispose(); page = Blank(landscape); gfx = XGraphics.FromPdfPage(page); wide = landscape; y = 78; if (title != null) Heading(title, 12);}
        private void Mark(string title) => contents.Add((title, pdf.PageCount));
        private void Divider(string title) {NewPage(); Mark(title); y = Height * .43; Heading(title, 17, true);}
        private void Heading(string text, double size = 14, bool centered = false) {
            var font = Font(size, true, true); var lines = Wrap(Clean(text), font, BodyWidth);
            foreach (var line in lines) {if (y + size * 1.6 > Height - 60) NewPage(wide); gfx.DrawString(line, font, XBrushes.Black, new XRect(Margin, y, BodyWidth, size * 1.5), centered ? XStringFormats.TopCenter : XStringFormats.TopLeft); y += size * 1.5;}
            y += 10;
        }
        private void Paragraph(string text, string? keepTogetherCaption = null) {
            var font = Font(11, serif: true);
            var lines = Wrap(Clean(text), font, BodyWidth);
            if (keepTogetherCaption != null && lines.Count * 16 + 10 < Height - 150 && y + lines.Count * 16 + 10 > Height - 60) NewPage(wide, keepTogetherCaption);
            foreach (var line in lines) {if (y + 16 > Height - 60) NewPage(wide); gfx.DrawString(line, font, XBrushes.Black, Margin, y + 12); y += 16;}
            y += 10;
        }
        private List<string> Wrap(string text, XFont font, double width) {
            var lines = new List<string>();
            foreach (var paragraph in text.Split('\n')) {
                var line = "";
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
                    if (gfx.MeasureString(line.Length == 0 ? word : line + " " + word, font).Width <= width) {line = line.Length == 0 ? word : line + " " + word; continue;}
                    if (line.Length > 0) {lines.Add(line); line = "";}
                    foreach (var c in word) {if (gfx.MeasureString(line + c, font).Width > width && line.Length > 0) {lines.Add(line); line = "";} line += c;}
                }
                lines.Add(line);
            }
            return lines.Count == 0 ? ["-"] : lines;
        }
        private void Table(string[] headers, double[] weights, IEnumerable<Cell[]> source, double size, string caption, bool landscape, XBrush? headerBrush = null, Action? continuationHeading = null, bool centered = false) {
            var widths = weights.Select(w => BodyWidth * w / weights.Sum()).ToArray();
            var font = Font(size); var headFont = Font(size, true); var lineHeight = size * 1.35;
            var headerLines = headers.Select((t, n) => Wrap(t, headFont, widths[n] - 8)).ToArray();
            var headerHeight = headerLines.Max(l => l.Count) * lineHeight + 12;
            var tableTop = y;
            void NextPage() {if (continuationHeading is null) NewPage(landscape, caption); else {NewPage(landscape); continuationHeading();}}
            void Head() {DrawRow(headerLines, widths, headerHeight, headFont, lineHeight, true, null, headerBrush, centered);}
            if (y + headerHeight + 24 > Height - 60) NextPage();
            Head(); var any = false;
            foreach (var cells in source) {
                ct.ThrowIfCancellationRequested(); any = true;
                var lines = cells.Select((cell, n) => Wrap(cell.Image is null ? Clean(cell.Text) : "", font, widths[n] - 8)).ToArray();
                var maxLines = Math.Max(1, (int)((Height - (continuationHeading is null ? 160 : tableTop + 70) - headerHeight) / lineHeight));
                var chunks = (int)Math.Ceiling(lines.Max(l => l.Count) / (double)maxLines);
                for (var part = 0; part < chunks; part++) {
                    var chunk = lines.Select(l => l.Skip(part * maxLines).Take(maxLines).ToList()).ToArray();
                    var images = part == 0 ? cells.Select(c => c.Image).ToArray() : null;
                    var height = Math.Max(22, chunk.Max(l => l.Count) * lineHeight + 10);
                    if (images?.Any(i => i != null) == true) height = Math.Max(height, 110);
                    if (y + height > Height - 60) {NextPage(); Head();}
                    DrawRow(chunk, widths, height, font, lineHeight, false, images, centered: centered);
                }
            }
            if (!any) {y += 14; Paragraph("No data available");}
        }
        private void DrawRow(List<string>[] lines, double[] widths, double height, XFont font, double lineHeight, bool header, ReportAsset?[]? images, XBrush? headerBrush = null, bool centered = false) {
            var x = Margin;
            for (var n = 0; n < widths.Length; n++) {
                var rect = new XRect(x, y, widths[n], height);
                gfx.DrawRectangle(Grid, header ? headerBrush ?? Blue : XBrushes.White, rect);
                if (images?.ElementAtOrDefault(n) is ReportAsset image) DrawImage(image, new XRect(x + 4, y + 4, widths[n] - 8, height - 8));
                else for (var l = 0; l < lines[n].Count; l++) {
                    if (centered) gfx.DrawString(lines[n][l], font, XBrushes.Black,
                        new XRect(x + 4, y + (height - lines[n].Count * lineHeight) / 2 + l * lineHeight, widths[n] - 8, lineHeight), XStringFormats.TopCenter);
                    else gfx.DrawString(lines[n][l], font, XBrushes.Black, x + 4, y + 6 + font.Size + l * lineHeight);
                }
                x += widths[n];
            }
            y += height;
        }
        private void RegisterHeading(string title) {Heading(title, 13, true); Paragraph($"Vessel / Site: {Clean(report.SiteName)}     Location: {Clean(report.Location)}\nInspector / Prepared By: {Clean(report.PreparedBy)}");}
        private void DefectRegisterHeading() {
            const double height = 54;
            var titleWidth = BodyWidth * .64;
            var detailsWidth = BodyWidth - titleWidth;
            var logoWidth = BodyWidth * .075;
            var detailsX = Margin + titleWidth;
            gfx.DrawRectangle(Grid, Margin, y, titleWidth, height);
            CoverText("EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER",
                new XRect(Margin + 8, y, titleWidth - logoWidth - 16, height), 13, true, true);
            if (report.OurCompanyLogo is not null) DrawImage(report.OurCompanyLogo,
                new XRect(detailsX - logoWidth + 4, y + 6, logoWidth - 10, height - 12), blankIfUnavailable: true);
            var details = new[] {"Ex-Surveyor : " + Clean(report.PreparedBy), "Vessel / Site : " + Clean(report.SiteName), "Location : " + Clean(report.Location)};
            for (var row = 0; row < details.Length; row++) {
                var rowY = y + row * height / 3;
                gfx.DrawRectangle(Grid, detailsX, rowY, detailsWidth, height / 3);
                CoverText(details[row], new XRect(detailsX + 5, rowY + 2, detailsWidth - 10, height / 3 - 4), 8);
            }
            y += height;
        }
        private void EquipmentRegisterHeading() {
            const double height = 54;
            var clientWidth = BodyWidth * .095;
            var companyWidth = BodyWidth * .075;
            var detailsWidth = BodyWidth * .21;
            var titleWidth = BodyWidth - clientWidth - companyWidth - detailsWidth;
            var titleX = Margin + clientWidth;
            var companyX = titleX + titleWidth;
            var detailsX = companyX + companyWidth;
            gfx.DrawRectangle(Grid, Margin, y, clientWidth, height);
            gfx.DrawRectangle(Grid, titleX, y, titleWidth, height);
            gfx.DrawRectangle(Grid, companyX, y, companyWidth, height);
            CoverText("HAZARDOUS AREA EQUIPMENT REGISTER", new XRect(titleX + 8, y, titleWidth - 16, height), 13, true, true);
            if (report.ClientLogo is not null) DrawImage(report.ClientLogo, new XRect(Margin + 6, y + 6, clientWidth - 12, height - 12), blankIfUnavailable: true);
            if (report.OurCompanyLogo is not null) DrawImage(report.OurCompanyLogo, new XRect(companyX + 5, y + 6, companyWidth - 10, height - 12), blankIfUnavailable: true);
            var details = new[] {"Vessel/Site : " + Clean(report.SiteName), "Location : " + Clean(report.Location), "Inspector : " + Clean(report.PreparedBy)};
            for (var row = 0; row < details.Length; row++) {
                var rowY = y + row * height / 3;
                gfx.DrawRectangle(Grid, detailsX, rowY, detailsWidth, height / 3);
                CoverText(details[row], new XRect(detailsX + 5, rowY + 2, detailsWidth - 10, height / 3 - 4), 8, true, true);
            }
            y += height;
        }
        private void DrawImage(ReportAsset asset, XRect box, IReadOnlyList<ReportEquipment>? pins = null, bool blankIfUnavailable = false) {
            try {
                if (!bytes.TryGetValue(asset.Key, out var data) || data is null) throw new InvalidDataException("Missing image");
                data = Orient(data);
                using var buffer = new MemoryStream(data);
                XImage image;
                try {image = XImage.FromStream(buffer);}
                catch {using var bitmap = SKBitmap.Decode(data) ?? throw new InvalidDataException("Unreadable image"); using var decoded = SKImage.FromBitmap(bitmap); using var png = decoded.Encode(SKEncodedImageFormat.Png, 100); image = XImage.FromStream(new MemoryStream(png.ToArray()));}
                using (image) {
                    var scale = Math.Min(box.Width / image.PixelWidth, box.Height / image.PixelHeight);
                    var w = image.PixelWidth * scale; var h = image.PixelHeight * scale;
                    var x = box.X + (box.Width - w) / 2; var top = box.Y + (box.Height - h) / 2;
                    gfx.DrawImage(image, x, top, w, h);
                    if (pins != null) foreach (var pin in pins) {
                        var px = x + Math.Clamp((double)pin.XPercent, 0, 100) / 100 * w;
                        var py = top + Math.Clamp((double)pin.YPercent, 0, 100) / 100 * h;
                        var label = pin.SequenceNo.ToString(); var radius = Math.Max(8, gfx.MeasureString(label, Font(8, true)).Width / 2 + 3);
                        gfx.DrawEllipse(new XPen(XColors.White, 1), XBrushes.Red, px - radius, py - radius, radius * 2, radius * 2);
                        gfx.DrawString(label, Font(8, true), XBrushes.White, new XRect(px - radius, py - radius, radius * 2, radius * 2), XStringFormats.Center);
                    }
                }
            }
            catch (Exception ex) {logger.LogWarning(ex, "Report image unavailable {AssetKey}", asset.Key); if (!blankIfUnavailable) gfx.DrawString("Image unavailable", Font(9), XBrushes.Gray, box, XStringFormats.Center);}
        }
        private static byte[] Orient(byte[] data) {
            using var codec = SKCodec.Create(new MemoryStream(data));
            if (codec is null || codec.EncodedOrigin == SKEncodedOrigin.TopLeft) return data;
            using var source = SKBitmap.Decode(data) ?? throw new InvalidDataException("Unreadable image");
            var swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            using var oriented = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
            using var canvas = new SKCanvas(oriented);
            switch (codec.EncodedOrigin) {
                case SKEncodedOrigin.TopRight: canvas.Translate(oriented.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(oriented.Width, oriented.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, oriented.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(oriented.Width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(oriented.Width, oriented.Height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, oriented.Height); canvas.RotateDegrees(270); break;
            }
            canvas.DrawBitmap(source, 0, 0);
            using var image = SKImage.FromBitmap(oriented); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
        private void EquipmentIndex(ReportArea area) {
            var equipment = area.Equipment.OrderBy(item => item.SequenceNo).ToArray();
            var index = 0;
            const double gap = 18;
            const double headerHeight = 24;
            while (index < equipment.Length) {
                ct.ThrowIfCancellationRequested();
                NewPage(true); Heading("AREA EQUIPMENT INDEX - " + area.Name);
                var blockWidth = (BodyWidth - gap * 2) / 3;
                double[] widths = [blockWidth * .14, blockWidth * .22, blockWidth * .64];
                var tableTop = y;
                var bottom = Height - 65;
                for (var block = 0; block < 3; block++) {
                    var blockX = Margin + block * (blockWidth + gap);
                    var rowY = tableTop;
                    DrawCells(["Sl. No", "Item #", "Description"], 9, true, headerHeight);
                    rowY += headerHeight;
                    for (var row = 0; row < 30 && index < equipment.Length; row++) {
                        var item = equipment[index];
                        var texts = new[] {item.SequenceNo.ToString(), EquipmentId(item), Clean(item.Type)};
                        var size = 8.0;
                        var font = Font(size, serif: true);
                        var lineCount = texts.Select((text, column) => Wrap(text, font, widths[column] - 8).Count).Max();
                        var height = Math.Max(18, lineCount * size * 1.15 + 6);
                        if (row > 0 && rowY + height > bottom) break;
                        while (rowY + height > bottom) {
                            size *= .95; font = Font(size, serif: true);
                            lineCount = texts.Select((text, column) => Wrap(text, font, widths[column] - 8).Count).Max();
                            height = Math.Max(18, lineCount * size * 1.15 + 6);
                        }
                        DrawCells(texts, size, false, height);
                        rowY += height;
                        index++;
                    }
                    void DrawCells(string[] texts, double size, bool header, double height) {
                        var x = blockX;
                        var font = Font(size, header, serif: true);
                        for (var column = 0; column < 3; column++) {
                            if (header) gfx.DrawRectangle(Blue, x, rowY, widths[column], height);
                            gfx.DrawRectangle(Grid, x, rowY, widths[column], height);
                            var lines = Wrap(texts[column], font, widths[column] - 8);
                            var textY = rowY + (height - lines.Count * size * 1.15) / 2;
                            foreach (var line in lines) {
                                gfx.DrawString(line, font, XBrushes.Black, new XRect(x + 4, textY, widths[column] - 8, size * 1.15), XStringFormats.TopCenter);
                                textY += size * 1.15;
                            }
                            x += widths[column];
                        }
                    }
                }
            }
        }
        private void Plan(ReportAsset plan) {
            if (plan.ContentType != "application/pdf") {NewPage(true); Heading("HAZARDOUS AREA PLAN"); DrawImage(plan, new XRect(Margin, y + 10, BodyWidth, Height - y - 80)); return;}
            try {
                if (!bytes.TryGetValue(plan.Key, out var data) || data is null) throw new InvalidDataException("Missing PDF plan");
                using var source = new MemoryStream(data);
                using var form = XPdfForm.FromStream(source);
                for (var n = 1; n <= form.PageCount; n++) {
                    ct.ThrowIfCancellationRequested(); form.PageNumber = n; NewPage(form.PointWidth > form.PointHeight); Heading("HAZARDOUS AREA PLAN", 13);
                    var scale = Math.Min(BodyWidth / form.PointWidth, (Height - y - 65) / form.PointHeight);
                    gfx.DrawImage(form, Margin + (BodyWidth - form.PointWidth * scale) / 2, y, form.PointWidth * scale, form.PointHeight * scale);
                }
            }
            catch (OperationCanceledException) {throw;}
            catch (Exception ex) {logger.LogWarning(ex, "Hazardous area plan could not be embedded"); NewPage(); Paragraph("Uploaded Hazardous Area Plan could not be embedded. The original remains available in Project Details.");}
        }
        private void Contents() {
            var index = 0;
            foreach (var p in toc) {
                page = p; gfx = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append); y = 78; Heading("CONTENTS", 15);
                foreach (var (label, number) in contents.Skip(index)) {
                    var font = Font(10, serif: true); var text = Wrap(label, font, BodyWidth - 45);
                    if (y + text.Count * 15 + 6 > Height - 60) break;
                    foreach (var line in text) {gfx.DrawString(line, font, XBrushes.Black, Margin, y); y += 15;}
                    gfx.DrawString(number.ToString(), font, XBrushes.Black, new XRect(Width - Margin - 36, y - text.Count * 15 - 11, 36, 16), XStringFormats.TopRight);
                    y += 6; index++;
                }
                gfx.Dispose(); gfx = null!;
            }
        }
        private void Cover(PdfPage p) {
            page = p; gfx = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
            using var background = typeof(ProjectReportPdf).Assembly.GetManifestResourceStream("ExSurvey.ReportCover")
                ?? throw new InvalidOperationException("The report cover template is missing.");
            using var image = XImage.FromStream(background);
            // Coordinate map matches the supplied artwork; embed its original pixels unchanged.
            var scale = Math.Min(Width / image.PixelWidth, Height / image.PixelHeight);
            var left = (Width - image.PixelWidth * scale) / 2;
            var top = (Height - image.PixelHeight * scale) / 2;
            gfx.DrawImage(image, left, top, image.PixelWidth * scale, image.PixelHeight * scale);
            XRect Box(double x, double y, double width, double height) => new(left + x * scale, top + y * scale, width * scale, height * scale);
            // Keep the template's top-right ARIES logo untouched.
            CoverProjectName();
            // Use the supplied table's exact cell bounds, without covering or redrawing its artwork.
            var company = report.OurCompany;
            var contactLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(company?.Address)) contactLines.Add(company.Address);
            if (!string.IsNullOrWhiteSpace(company?.Telephone)) contactLines.Add("Tel: " + company.Telephone);
            if (!string.IsNullOrWhiteSpace(company?.Fax)) contactLines.Add("Fax: " + company.Fax);
            if (!string.IsNullOrWhiteSpace(company?.Website)) contactLines.Add(company.Website);
            if (!string.IsNullOrWhiteSpace(company?.Email)) contactLines.Add(company.Email);
            CompanyCoverText(company?.CompanyName ?? "OUR COMPANY DETAILS", contactLines, Box(65, 1173, 280, 112));
            if (report.OurCompanyLogo is not null) DrawImage(report.OurCompanyLogo, Box(355, 1180, 155, 98));
            else CoverText("No logo available", Box(355, 1180, 155, 98), 6, false, true);
            if (report.ClientLogo is not null) {
                DrawImage(report.ClientLogo, Box(555, 1175, 425, 65));
                CoverText(report.Client, Box(545, 1248, 445, 34), 11, true, true);
            }
            else CoverText("CLIENT\n" + Clean(report.Client), Box(545, 1175, 445, 105), 13, true, true);
            var rows = new[] {
                new[] {"Prepared by", report.PreparedBy, "UID Number", Clean(report.UidNumber), "Date", report.GeneratedAt.ToString("dd.MM.yy")},
                new[] {"Checked by", report.CheckedBy, "Job Number", report.JobNumber, "Total Pages", pdf.PageCount.ToString()},
                new[] {"Approved by", report.ApprovedBy, "Document Number", Clean(report.DocumentNumber), "Revision", report.Revision ?? (report.Revisions.Count == 0 ? "-" : report.Revisions.Last().Revision.ToString())}
            };
            double[] columns = [47, 227, 368, 528, 767, 872, 1008];
            double[] rowEdges = [1299, 1347, 1398, 1449];
            for (var r = 0; r < rows.Length; r++) for (var c = 0; c < rows[r].Length; c++) {
                CoverText(rows[r][c], Box(columns[c] + 8, rowEdges[r] + 5, columns[c + 1] - columns[c] - 16, rowEdges[r + 1] - rowEdges[r] - 10), 8.5, c % 2 == 0);
            }
            gfx.Dispose(); gfx = null!;
        }
        private void CoverProjectName() {
            var title = Clean(report.ProjectName).ToUpperInvariant();
            var width = Width - 100;
            var size = 24.0;
            var font = Font(size, true, serif: true);
            var lines = Wrap(title, font, width);
            while (lines.Count > 2 || lines.Any(line => gfx.MeasureString(line, font).Width > width)) {
                size *= .95;
                font = Font(size, true, serif: true);
                lines = Wrap(title, font, width);
            }
            var lineHeight = size * 1.2;
            var textY = (Height - lines.Count * lineHeight) / 2;
            foreach (var line in lines) {
                gfx.DrawString(line, font, XBrushes.Black, new XRect(50, textY, width, lineHeight), XStringFormats.TopCenter);
                textY += lineHeight;
            }
        }
        private void CompanyCoverText(string name, IReadOnlyList<string> details, XRect box) {
            // Fit both columns inside the existing cell; keep the heading and all details top-left aligned.
            var size = 8.0;
            List<string> headingLines, detailLines;
            XFont headingFont, detailFont;
            double headingSize, lineHeight, headingHeight;
            do {
                headingSize = size * 1.3;
                headingFont = Font(headingSize, true);
                detailFont = Font(size);
                headingLines = Wrap(name, headingFont, box.Width);
                detailLines = details.SelectMany(detail => Wrap(detail, detailFont, box.Width)).ToList();
                lineHeight = size * 1.08;
                headingHeight = headingLines.Count * headingSize * 1.08 + 2;
                if (headingHeight + detailLines.Count * lineHeight <= box.Height || size <= 3) break;
                size -= .25;
            } while (true);
            var textY = box.Y;
            foreach (var line in headingLines) {
                gfx.DrawString(line, headingFont, XBrushes.Black, new XRect(box.X, textY, box.Width, headingSize * 1.08), XStringFormats.TopLeft);
                textY += headingSize * 1.08;
            }
            textY += 2;
            foreach (var line in detailLines) {
                gfx.DrawString(line, detailFont, XBrushes.Black, new XRect(box.X, textY, box.Width, lineHeight), XStringFormats.TopLeft);
                textY += lineHeight;
            }
        }
        private void CoverText(string? value, XRect box, double size, bool bold = false, bool centered = false) {
            var font = Font(size, bold, serif: true);
            var lines = Wrap(Clean(value), font, box.Width);
            while ((lines.Count * size * 1.2 > box.Height || lines.Any(line => gfx.MeasureString(line, font).Width > box.Width)) && size > 2) {
                size -= .5; font = Font(size, bold, serif: true); lines = Wrap(Clean(value), font, box.Width);
            }
            var textY = box.Y + (box.Height - lines.Count * size * 1.2) / 2;
            foreach (var line in lines) {
                gfx.DrawString(line, font, XBrushes.Black, new XRect(box.X, textY, box.Width, size * 1.2), centered ? XStringFormats.TopCenter : XStringFormats.TopLeft);
                textY += size * 1.2;
            }
        }
        private void Introduction() {
            var x = Margin + 10;
            var textWidth = BodyWidth - 20;
            var headingFont = Font(14, true, serif: true);
            gfx.DrawString("1. Introduction", headingFont, XBrushes.Black, new XRect(x, y, textWidth, 19), XStringFormats.TopLeft);
            gfx.DrawLine(new XPen(XColors.Black, .7), x, y + 17, x + gfx.MeasureString("1. Introduction", headingFont).Width, y + 17);
            y += 34;
            Mark("1.1 Executive Summary");
            Subheading("1.1     Executive Summary");
            var dates = report.Areas.SelectMany(a => a.Equipment).SelectMany(e => e.Inspections)
                .Where(i => i.Date.HasValue).Select(i => i.Date!.Value).Order().ToArray();
            var start = dates.Length == 0 ? "-" : Date(dates.First());
            var end = dates.Length == 0 ? "-" : Date(dates.Last());
            Justified($"This report has been written for {Clean(report.Client)} {Clean(report.SiteName)} as a result of a survey conducted from {start} to {end}. This report specifies what has been inspected and in what manner. Our Work scope was to 100% Detailed inspection of all electrical equipment’s in the hazardous area and give an independent analysis of all Ex- rated equipment so that our client {Clean(report.Client)}, would have a good idea of the condition of the rig. This survey included all safety critical equipment and systems and where possible equipment was opened up to enable equipment details to be taken. The co-operation from the rig staff was excellent and we would like to thank them for their assistance and patience during what was busy time.");
            y += 8;
            Mark("1.2 Applicable Standards and References");
            Subheading("1.2     Applicable Standards and References");
            Justified("The criteria which have been used as reference during this survey are internationally recognized standards, local legislative requirements, client’s safety and operating standards, the original equipment manufacturer’s maintenance and operating specifications and accepted oilfield operating and safety practices.");
            y += 30;
            var standardsFont = new XFont("Report Serif", 11, XFontStyleEx.BoldItalic);
            gfx.DrawString("1.2.1   Following standards shall be followed for this EX. Survey", standardsFont, XBrushes.Black,
                new XRect(x + 18, y, textWidth - 18, 18), XStringFormats.TopLeft);
            y += 28;
            foreach (var standard in new[] {
                "IEC 600 79-10 (Classification of hazardous areas)",
                "IEC 600 79-14 (Electrical installations in hazardous areas)",
                "IEC 600 79-17 (Inspection and maintenance of electrical equipment in hazardous areas)"
            }) {
                var bulletX = x + 18;
                gfx.DrawLines(new XPen(XColors.Black, .6), new[] {
                    new XPoint(bulletX, y + 3), new XPoint(bulletX + 6, y + 6), new XPoint(bulletX, y + 9),
                    new XPoint(bulletX + 2, y + 6), new XPoint(bulletX, y + 3)
                });
                var lines = Wrap(standard, Font(11, serif: true), textWidth - 36);
                foreach (var line in lines) {
                    gfx.DrawString(line, Font(11, serif: true), XBrushes.Black, new XRect(x + 36, y, textWidth - 36, 18), XStringFormats.TopLeft);
                    y += 18;
                }
                y += 10;
            }
            void Subheading(string title) {
                if (y + 40 > Height - 60) NewPage();
                gfx.DrawString(title, Font(12, true, serif: true), XBrushes.Black, new XRect(x, y, textWidth, 18), XStringFormats.TopLeft);
                y += 28;
            }
            void Justified(string paragraph) {
                var font = Font(11, serif: true);
                var lines = Wrap(paragraph, font, textWidth);
                for (var index = 0; index < lines.Count; index++) {
                    if (y + 18 > Height - 60) NewPage();
                    var words = lines[index].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (index == lines.Count - 1 || words.Length < 2) {
                        gfx.DrawString(lines[index], font, XBrushes.Black, new XRect(x, y, textWidth, 18), XStringFormats.TopLeft);
                    } else {
                        var spacing = (textWidth - words.Sum(word => gfx.MeasureString(word, font).Width)) / (words.Length - 1);
                        var wordX = x;
                        foreach (var word in words) {
                            gfx.DrawString(word, font, XBrushes.Black, new XRect(wordX, y, gfx.MeasureString(word, font).Width + .1, 18), XStringFormats.TopLeft);
                            wordX += gfx.MeasureString(word, font).Width + spacing;
                        }
                    }
                    y += 18;
                }
            }
        }
        private void RevisionHistory() {
            var tableWidth = BodyWidth * .92;
            var tableLeft = (Width - tableWidth) / 2;
            const double titleHeight = 34;
            gfx.DrawRectangle(new XPen(XColors.Black, 1), tableLeft, y, tableWidth, titleHeight);
            gfx.DrawString("REVISION HISTORY", Font(16, true, serif: true), XBrushes.Black,
                new XRect(tableLeft, y, tableWidth, titleHeight), XStringFormats.Center);
            y += titleHeight;
            double[] weights = [1.1, 1, 1.5, 3.1, 1.4, 1.4, 1.4];
            var widths = weights.Select(weight => tableWidth * weight / weights.Sum()).ToArray();
            var values = new[] {Clean(report.UidNumber), Clean(report.Revision), Date(report.GeneratedAt),
                Clean(report.Description), Clean(report.PreparedBy), Clean(report.CheckedBy), Clean(report.ApprovedBy)};
            var valueHeight = Math.Min(Height - y - 100, Math.Max(40, values.Select((value, column) => Wrap(value, Font(10, serif: true), widths[column] - 12).Count * 12 + 16).Max()));
            Row(["UID", "REV", "DATE", "DESCRIPTION", "PRE", "CHK", "APR"], 32, 11, true);
            Row(values, valueHeight, 10, false);
            void Row(string[] texts, double height, double initialSize, bool bold) {
                var x = tableLeft;
                for (var column = 0; column < texts.Length; column++) {
                    var size = initialSize;
                    var font = Font(size, bold, serif: true);
                    var lines = Wrap(texts[column], font, widths[column] - 12);
                    while (lines.Count * size * 1.2 > height - 12) {
                        size *= .95; font = Font(size, bold, serif: true);
                        lines = Wrap(texts[column], font, widths[column] - 12);
                    }
                    gfx.DrawRectangle(new XPen(XColors.Black, 1), x, y, widths[column], height);
                    var textY = y + (height - lines.Count * size * 1.2) / 2;
                    foreach (var line in lines) {
                        gfx.DrawString(line, font, XBrushes.Black, new XRect(x + 6, textY, widths[column] - 12, size * 1.2), XStringFormats.TopCenter);
                        textY += size * 1.2;
                    }
                    x += widths[column];
                }
                y += height;
            }
        }
        private void Headers() {
            for (var n = 1; n < pdf.PageCount; n++) {
                var p = pdf.Pages[n]; using var drawing = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
                var width = p.Width.Point; var height = p.Height.Point;
                var font = Font(9, serif: true, italic: true);
                HeaderText(report.ProjectName, Margin, false);
                HeaderText(report.SiteName, width / 2 + 6, true);
                var line = new XPen(XColor.FromArgb(171, 169, 155), 2.5);
                drawing.DrawLine(line, Margin, 52, width - Margin, 52);
                drawing.DrawLine(line, Margin, height - 40, width - Margin, height - 40);
                drawing.DrawString("ARIES MARINE", font, XBrushes.Black, Margin, height - 23);
                drawing.DrawString($"Page {n + 1} of {pdf.PageCount}", font, XBrushes.Black, new XRect(width - Margin - 120, height - 34, 120, 18), XStringFormats.TopRight);
                void HeaderText(string? value, double x, bool right) {
                    var text = Clean(value);
                    var availableWidth = width / 2 - Margin - 6;
                    var size = 9.0;
                    var headerFont = font;
                    while (drawing.MeasureString(text, headerFont).Width > availableWidth) {
                        size *= .95; headerFont = Font(size, serif: true, italic: true);
                    }
                    var textX = right ? width - Margin - drawing.MeasureString(text, headerFont).Width : x;
                    drawing.DrawString(text, headerFont, XBrushes.Black, textX, 49);
                }
            }
        }
        public void Dispose() {gfx?.Dispose(); pdf.Dispose();}
    }

    private sealed class ReportFonts : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new((familyName == "Report Serif" ? "serif" : "sans") + (bold ? "-bold" : "") + (italic ? "-italic" : ""));
        public byte[] GetFont(string faceName) {
            var serif = faceName.StartsWith("serif"); var bold = faceName.Contains("bold"); var italic = faceName.Contains("italic");
            var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), serif ? bold && italic ? "timesbi.ttf" : bold ? "timesbd.ttf" : italic ? "timesi.ttf" : "times.ttf" : bold && italic ? "arialbi.ttf" : bold ? "arialbd.ttf" : italic ? "ariali.ttf" : "arial.ttf");
            var unix = "/usr/share/fonts/truetype/dejavu/" + (serif ? "DejaVuSerif" : "DejaVuSans") + (bold && italic ? serif ? "-BoldItalic" : "-BoldOblique" : bold ? "-Bold" : italic ? serif ? "-Italic" : "-Oblique" : "") + ".ttf";
            var path = File.Exists(windows) ? windows : unix;
            if (!File.Exists(path)) throw new InvalidOperationException("Report fonts are missing. Install Arial/Times New Roman on Windows or DejaVu fonts on Linux.");
            return File.ReadAllBytes(path);
        }
    }
}
