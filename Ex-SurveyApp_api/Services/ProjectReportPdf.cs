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
        lock (FontLock) {GlobalFontSettings.FontResolver ??= new ReportFonts();}
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
            Heading("REVISION HISTORY", 13, true);
            Table(["UID", "REV", "DATE", "DESCRIPTION", "PRE", "CHK", "APR"], [1, .7, 1.4, 3.3, 1.4, 1.4, 1.4],
                report.Revisions.Count == 0 ? [Cells("-", "-", Date(report.GeneratedAt), "Generated from recorded project and inspection data", report.PreparedBy, report.CheckedBy, report.ApprovedBy)] :
                    report.Revisions.Select(r => Cells("-", r.Revision.ToString(), Date(r.GeneratedAt), "-", "-", "-", "-")), 9, "REVISION HISTORY", false, XBrushes.White);
            var expectedContents = new[] {"1. Introduction", "1.1 Executive Summary", "1.2 Applicable Standards and References", "2. HAZARDOUS AREA PLAN", "3. HAZARDOUS AREA EQUIPMENT REGISTER & EQUIPMENT LAYOUT"}
                .Concat(report.Areas.Select((a, n) => $"3.{n + 1} {a.Name}"))
                .Concat(new[] {"4. EQUIPMENT DEFECT & RECOMMENDATION REGISTER", "5. EQUIPMENT CHECK LIST (IEC 60079-17)"})
                .Concat(report.Areas.Select((a, n) => $"5.{n + 1} {a.Name}"));
            var tocY = 110.5;
            toc.Add(Blank());
            foreach (var label in expectedContents) {
                var height = Wrap(label, Font(10, serif: true), BodyWidth - 45).Count * 15 + 6;
                if (tocY + height > Height - 60) {toc.Add(Blank()); tocY = 110.5;}
                tocY += height;
            }
            NewPage(); Mark("1. Introduction");
            Heading("1. Introduction", 15);
            Mark("1.1 Executive Summary"); Heading("1.1 Executive Summary", 12);
            var inspections = report.Areas.SelectMany(a => a.Equipment).SelectMany(e => e.Inspections).ToArray();
            var dates = inspections.Where(i => i.Date.HasValue).Select(i => i.Date!.Value).Order().ToArray();
            Paragraph($"This report presents recorded explosion proof survey information for {Clean(report.SiteName)} ({Clean(report.ProjectName)}), at {Clean(report.Location)}, for {Clean(report.Client)}. Job number: {Clean(report.JobNumber)}. Recorded inspection period: {(dates.Length == 0 ? "-" : Date(dates.First()) + " to " + Date(dates.Last()))}.");
            Paragraph($"The project contains {report.Areas.Count} areas, {report.Areas.Sum(a => a.Equipment.Count)} equipment items and {inspections.Length} inspection records. Equipment details, layout marker positions, saved checklist responses, photographs, findings and QC results are reproduced from the application. Uninspected equipment and pending reviews are identified as recorded; missing information is shown as '-'.");
            Mark("1.2 Applicable Standards and References"); Heading("1.2 Applicable Standards and References", 12);
            Paragraph("The following references are retained from the supplied approved report template. The recorded inspection type and checklist answers determine the content of each equipment inspection record.");
            Paragraph("IEC 60079-10 - Classification of hazardous areas\nIEC 60079-14 - Electrical installations in hazardous areas\nIEC 60079-17 - Inspection and maintenance of electrical equipment in hazardous areas");
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
                NewPage(true); Heading($"3.{a + 1} {area.Name} - EQUIPMENT LAYOUT");
                if (area.Layout is null) Paragraph("No Area Layout Uploaded");
                else DrawImage(area.Layout, new XRect(Margin, y + 10, BodyWidth, Height - y - 80), area.Equipment);
                if (area.Equipment.Count == 0) continue;
                NewPage(true); Heading("AREA EQUIPMENT INDEX - " + area.Name);
                var indexRows = (int)Math.Ceiling(area.Equipment.Count / 3d);
                Table(["Sl. No", "Item / Equipment ID", "Description / Type", "Sl. No", "Item / Equipment ID", "Description / Type", "Sl. No", "Item / Equipment ID", "Description / Type"],
                    [.5, 1.2, 3, .5, 1.2, 3, .5, 1.2, 3], Enumerable.Range(0, indexRows).Select(row => Enumerable.Range(0, 3).SelectMany(col => {
                        var item = area.Equipment.ElementAtOrDefault(row + col * indexRows);
                        return item is null ? Cells("-", "-", "-") : Cells(item.SequenceNo.ToString(), EquipmentId(item), item.Type);
                    }).ToArray()), 8.5, "AREA EQUIPMENT INDEX - " + area.Name, true, XBrushes.White);
                NewPage(true); RegisterHeading("HAZARDOUS AREA EQUIPMENT REGISTER - " + area.Name);
                Table(["Sl. No", "Equipment ID", "Tag Number", "Area", "Type", "Location", "Manufacturer", "Circuit ID", "Protection Details", "Certificate Number", "Inspection Type", "Inspection Date", "Inspector", "Comments", "QC Status"],
                    [.55, 1, 1, .9, 1.2, 1, 1.2, .9, 1.3, 1.3, .85, .9, 1.1, 1.8, 1],
                    area.Equipment.SelectMany(e => e.Inspections.Count == 0 ? new[] {Register(e, area, null)} : e.Inspections.Select(i => Register(e, area, i))), 7.5, "HAZARDOUS AREA EQUIPMENT REGISTER - " + area.Name, true);
            }
            Divider("4. EQUIPMENT DEFECT & RECOMMENDATION REGISTER");
            NewPage(true); RegisterHeading("EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER");
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
                [.6, .9, 1, 1, .9, .85, 1.2, 2, 2, 1, 1.8, 1.8, 1.7], defects, 7.5, "EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER", true, new XSolidBrush(XColor.FromArgb(215, 222, 230)));
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
                    if (i.Photos.Count > 0) {
                        for (var n = 0; n < i.Photos.Count; n += 2) {
                            if (n > 0 || wide || y + 300 > Height - 60) NewPage();
                            Heading("INSPECTION PHOTOGRAPHS", 13);
                            Paragraph($"Equipment: {EquipmentId(e)} | Tag: {e.TagNumber} | Area: {area.Name}");
                            var space = (Height - y - 75) / Math.Min(2, i.Photos.Count - n);
                            for (var p = n; p < Math.Min(n + 2, i.Photos.Count); p++) {DrawImage(i.Photos[p], new XRect(Margin, y + 4, BodyWidth, space - 15)); y += space;}
                        }
                    }
                }
            }
            gfx?.Dispose(); gfx = null!;
            Cover(cover); Contents(); Headers();
            using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
        }

        private Cell[] Register(ReportEquipment e, ReportArea area, ReportInspection? i) => Cells(e.SequenceNo.ToString(), EquipmentId(e), e.TagNumber, area.Name, e.Type, e.Location,
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
        private void Table(string[] headers, double[] weights, IEnumerable<Cell[]> source, double size, string caption, bool landscape, XBrush? headerBrush = null) {
            var widths = weights.Select(w => BodyWidth * w / weights.Sum()).ToArray();
            var font = Font(size); var headFont = Font(size, true); var lineHeight = size * 1.35;
            var headerLines = headers.Select((t, n) => Wrap(t, headFont, widths[n] - 8)).ToArray();
            var headerHeight = headerLines.Max(l => l.Count) * lineHeight + 12;
            void Head() {DrawRow(headerLines, widths, headerHeight, headFont, lineHeight, true, null, headerBrush);}
            if (y + headerHeight + 24 > Height - 60) NewPage(landscape, caption);
            Head(); var any = false;
            foreach (var cells in source) {
                ct.ThrowIfCancellationRequested(); any = true;
                var lines = cells.Select((cell, n) => Wrap(cell.Image is null ? Clean(cell.Text) : "", font, widths[n] - 8)).ToArray();
                var maxLines = Math.Max(1, (int)((Height - 160 - headerHeight) / lineHeight));
                var chunks = (int)Math.Ceiling(lines.Max(l => l.Count) / (double)maxLines);
                for (var part = 0; part < chunks; part++) {
                    var chunk = lines.Select(l => l.Skip(part * maxLines).Take(maxLines).ToList()).ToArray();
                    var images = part == 0 ? cells.Select(c => c.Image).ToArray() : null;
                    var height = Math.Max(22, chunk.Max(l => l.Count) * lineHeight + 10);
                    if (images?.Any(i => i != null) == true) height = Math.Max(height, 110);
                    if (y + height > Height - 60) {NewPage(landscape, caption); Head();}
                    DrawRow(chunk, widths, height, font, lineHeight, false, images);
                }
            }
            if (!any) {y += 14; Paragraph("No data available");}
        }
        private void DrawRow(List<string>[] lines, double[] widths, double height, XFont font, double lineHeight, bool header, ReportAsset?[]? images, XBrush? headerBrush = null) {
            var x = Margin;
            for (var n = 0; n < widths.Length; n++) {
                var rect = new XRect(x, y, widths[n], height);
                gfx.DrawRectangle(Grid, header ? headerBrush ?? Blue : XBrushes.White, rect);
                if (images?.ElementAtOrDefault(n) is ReportAsset image) DrawImage(image, new XRect(x + 4, y + 4, widths[n] - 8, height - 8));
                else for (var l = 0; l < lines[n].Count; l++) gfx.DrawString(lines[n][l], font, XBrushes.Black, x + 4, y + 6 + font.Size + l * lineHeight);
                x += widths[n];
            }
            y += height;
        }
        private void RegisterHeading(string title) {Heading(title, 13, true); Paragraph($"Vessel / Site: {Clean(report.SiteName)}     Location: {Clean(report.Location)}\nInspector / Prepared By: {Clean(report.PreparedBy)}");}
        private void DrawImage(ReportAsset asset, XRect box, IReadOnlyList<ReportEquipment>? pins = null) {
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
            catch (Exception ex) {logger.LogWarning(ex, "Report image unavailable {AssetKey}", asset.Key); gfx.DrawString("Image unavailable", Font(9), XBrushes.Gray, box, XStringFormats.Center);}
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
        private void Plan(ReportAsset plan) {
            if (plan.ContentType != "application/pdf") {NewPage(); Heading("HAZARDOUS AREA PLAN"); DrawImage(plan, new XRect(Margin, y, BodyWidth, Height - y - 65)); return;}
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
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(30, 148, 191)), 0, 0, Width, 118);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(36, 45, 82)), 0, 118, Width, 22);
            gfx.DrawString("EX-SURVEY", Font(26, true), XBrushes.White, 35, 58);
            gfx.DrawString("FIELD INSPECTIONS & QUALITY REVIEW", Font(10), XBrushes.White, 35, 88);
            for (var row = 0; row < 16; row++) for (var col = 0; col < 9; col++) gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(240, 240, 240)), 12 + col * 20, 155 + row * 24, 10, 10);
            y = 265; Heading("EXPLOSION PROOF SURVEY REPORT", 24, true); Heading(Clean(report.SiteName), 23, true);
            y = 430; Heading(report.ProjectName, 16, true); Heading(report.Location, 11, true);
            y = Height - 270;
            Table(["EX-SURVEY", "CLIENT", "REPORT INFORMATION"], [1, 1, 1], [
                Cells("Prepared By: " + report.PreparedBy, report.Client, "Date: " + Date(report.GeneratedAt)),
                Cells("Checked By: " + report.CheckedBy, "Job Number: " + report.JobNumber, "Total Pages: " + pdf.PageCount),
                Cells("QC Approved By: " + report.ApprovedBy, "Document Number: -", "Revision: " + (report.Revisions.Count == 0 ? "-" : report.Revisions.Last().Revision.ToString())),
                Cells("UID: -", "Vessel / Site: " + report.SiteName, "Location: " + report.Location)
            ], 10, "REPORT INFORMATION", false, XBrushes.White);
            gfx.Dispose(); gfx = null!;
        }
        private void Headers() {
            for (var n = 1; n < pdf.PageCount; n++) {
                var p = pdf.Pages[n]; using var drawing = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
                var width = p.Width.Point; var height = p.Height.Point;
                var font = Font(9, serif: true, italic: true);
                drawing.DrawString("EXPLOSION PROOF SURVEY REPORT", font, XBrushes.Black, Margin, 32);
                drawing.DrawString(Clean(report.SiteName), font, XBrushes.Black, new XRect(width / 2, 22, width / 2 - Margin, 24), XStringFormats.TopRight);
                var line = new XPen(XColor.FromArgb(171, 169, 155), 2.5);
                drawing.DrawLine(line, Margin, 52, width - Margin, 52);
                drawing.DrawLine(line, Margin, height - 40, width - Margin, height - 40);
                drawing.DrawString("Ex-Survey", font, XBrushes.Black, Margin, height - 23);
                drawing.DrawString($"Page {n + 1} of {pdf.PageCount}", font, XBrushes.Black, new XRect(width - Margin - 120, height - 34, 120, 18), XStringFormats.TopRight);
            }
        }
        public void Dispose() {gfx?.Dispose(); pdf.Dispose();}
    }

    private sealed class ReportFonts : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new((familyName == "Report Serif" ? "serif" : "sans") + (bold ? "-bold" : italic ? "-italic" : ""));
        public byte[] GetFont(string faceName) {
            var serif = faceName.StartsWith("serif"); var bold = faceName.Contains("bold"); var italic = faceName.Contains("italic");
            var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), serif ? bold ? "timesbd.ttf" : italic ? "timesi.ttf" : "times.ttf" : bold ? "arialbd.ttf" : italic ? "ariali.ttf" : "arial.ttf");
            var unix = "/usr/share/fonts/truetype/dejavu/" + (serif ? "DejaVuSerif" : "DejaVuSans") + (bold ? "-Bold" : italic ? serif ? "-Italic" : "-Oblique" : "") + ".ttf";
            var path = File.Exists(windows) ? windows : unix;
            if (!File.Exists(path)) throw new InvalidOperationException("Report fonts are missing. Install Arial/Times New Roman on Windows or DejaVu fonts on Linux.");
            return File.ReadAllBytes(path);
        }
    }
}
