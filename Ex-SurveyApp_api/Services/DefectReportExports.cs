using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace Ex_SurveyApp_api.Services;

public sealed record DefectReportImage(byte[] Png, int Width, int Height);
public sealed class DefectReportMedia
{
    public Dictionary<Guid, DefectReportImage> Images { get; } = [];
    public DefectReportImage? ClientLogo { get; private set; }
    public DefectReportImage? CompanyLogo { get; private set; }
    public static DefectReportImage? Decode(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null) return null;
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new(png.ToArray(), bitmap.Width, bitmap.Height);
    }
    public static async Task<DefectReportMedia> Load(DefectRegisterReport report, IProjectPhotoStorage storage, CancellationToken ct)
    {
        async Task<DefectReportImage?> Read(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var stored = await storage.OpenReadAsync(key, ct);
            if (stored is null) return null;
            await using var source = stored.Stream; using var output = new MemoryStream();
            await source.CopyToAsync(output, ct); return Decode(output.ToArray());
        }
        var result = new DefectReportMedia { ClientLogo = await Read(report.ClientLogoPath) };
        using var company = CompanyAssets.Logo(report.CompanyLogoName) ?? CompanyAssets.Logo("aries_mar_engg.png");
        if (company is not null) { using var bytes = new MemoryStream(); await company.CopyToAsync(bytes, ct); result.CompanyLogo = Decode(bytes.ToArray()); }
        foreach (var photo in report.Items.SelectMany(row => row.Photos).DistinctBy(p => p.Id))
            if (photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && await Read(photo.StorageKey) is {} image) result.Images[photo.Id] = image;
        return result;
    }
}

public static class DefectReportExports
{
    public static readonly string[] Columns = ["ACTION NO", "DATE", "ITEM #", "AOSD ID", "FAULT CATEGORY A, B, C", "FAULT CODE", "AREA", "TYPE OF INSPECTION", "EQUIPMENT DESCRIPTION", "OBSERVATION", "RECOMMENDATION", "STATUS", "PHOTO", "PHOTO", "REMARKS", "REMARKS"];
    private static readonly double[] Weights = [55, 58, 49, 56, 66, 51, 60, 65, 100, 116, 110, 72, 110, 110, 90, 90];
    public static string[] Cells(DefectReportRow row) => [Value(row.ActionNo), row.Date?.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "—", Value(row.ItemNumber), string.IsNullOrWhiteSpace(row.AosdId) ? "NA" : row.AosdId,
        Value(row.FaultCategory), Value(row.FaultCode), Value(row.Area), Value(row.Grade), Value(row.EquipmentDescription), Value(row.Observation), Value(row.Recommendation), Value(row.Status),
        string.Join("\n", row.Photos.Where(p => p.Slot == 1).Select(p => p.FileName)), string.Join("\n", row.Photos.Where(p => p.Slot == 2).Select(p => p.FileName)), Value(row.Remarks), Value(row.AdditionalRemarks)];
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    public static byte[] Csv(DefectRegisterReport report)
    {
        static string Quote(string value)
        {
            var first = value.AsSpan().TrimStart();
            if (!first.IsEmpty && first[0] is '=' or '+' or '-' or '@') value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var rows = new[] { Columns }.Concat(report.Items.Select(Cells));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Quote)))) + "\r\n")).ToArray();
    }
    public static string StatusColor(string status)
    {
        if (status.Contains('\n')) {
            var colors = status.Split('\n').Select(StatusColor).ToArray();
            return colors.All(c => c == "C6EFCE") ? "C6EFCE" : colors.Contains("FFC7CE") ? "FFC7CE" : "FFEB9C";
        }
        return status switch {
        "Closed" or "ClientClosed" => "C6EFCE", "Open" or "Rejected" or "ClientRejected" or "Reopened" => "FFC7CE",
        "Proposed" or "InProgress" or "RectificationSubmitted" or "SubmittedForVerification" or "Verified" or "AwaitingAriesVerification" or "AwaitingClientClosure" => "FFEB9C", _ => "E5EDF5" };
    }
    public static byte[] Pdf(DefectRegisterReport report, DefectReportMedia media)
    {
        ProjectReportPdf.EnsureFonts();
        using var pdf = new PdfDocument(); pdf.Info.Title = "EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER"; pdf.Info.Creator = "Ex-Survey";
        var font = new XFont("Report Sans", 8); var bold = new XFont("Report Sans", 8, XFontStyleEx.Bold);
        var heading = new XFont("Report Sans", 12, XFontStyleEx.Bold);
        var grid = new XPen(XColors.Black, .5); var blue = new XSolidBrush(XColor.FromArgb(211, 225, 238));
        const double margin = 25, padding = 4, lineHeight = 10.5;
        XGraphics? gfx = null; double y = 0, bottom = 0, bodyStart = 0; double[] widths = [];
        var imageCache = new Dictionary<DefectReportImage, XImage>();
        void Image(DefectReportImage image, double x, double top, double width, double height)
        {
            if (!imageCache.TryGetValue(image, out var picture)) imageCache[image] = picture = XImage.FromStream(new MemoryStream(image.Png));
            var scale = Math.Min(width / image.Width, height / image.Height);
            gfx!.DrawImage(picture, x + (width - image.Width * scale) / 2, top + (height - image.Height * scale) / 2, image.Width * scale, image.Height * scale);
        }
        string[] Wrap(string text, XFont f, double width)
        {
            var lines = new List<string>();
            foreach (var paragraph in text.Replace("\r", "").Split('\n'))
            {
                var remaining = paragraph;
                while (remaining.Length > 0)
                {
                    var end = remaining.Length;
                    if (gfx!.MeasureString(remaining, f).Width > width)
                    {
                        var low = 1; var high = remaining.Length;
                        while (low < high) { var mid = (low + high + 1) / 2; if (gfx.MeasureString(remaining[..mid], f).Width <= width) low = mid; else high = mid - 1; }
                        end = Math.Max(1, low); var space = remaining.LastIndexOf(' ', end - 1); if (space > 0) end = space;
                        if (end < remaining.Length && char.IsHighSurrogate(remaining[end - 1]) && char.IsLowSurrogate(remaining[end])) end = end > 1 ? end - 1 : 2;
                    }
                    lines.Add(remaining[..end]); remaining = remaining[end..].TrimStart();
                }
                if (paragraph.Length == 0) lines.Add("");
            }
            return lines.ToArray();
        }
        void Cell(double x, double width, double height, string[] lines, XFont f, XBrush fill, bool center = false)
        {
            gfx!.DrawRectangle(grid, fill, x, y, width, height);
            for (var i = 0; i < lines.Length; i++) gfx.DrawString(lines[i], f, XBrushes.Black, new XRect(x + padding, y + padding + i * lineHeight, width - padding * 2, lineHeight), center ? XStringFormats.TopCenter : XStringFormats.TopLeft);
        }
        void Page()
        {
            gfx?.Dispose(); var page = pdf.AddPage(); page.Size = PageSize.A3; page.Orientation = PageOrientation.Landscape;
            gfx = XGraphics.FromPdfPage(page); y = margin; bottom = page.Height.Point - margin;
            var width = page.Width.Point - 2 * margin; widths = Weights.Select(w => width * w / Weights.Sum()).ToArray();
            const double headerHeight = 72; var left = 100d; var right = 340d; var logoWidth = 85d;
            gfx.DrawRectangle(grid, XBrushes.White, margin, y, left, headerHeight);
            if (media.ClientLogo is {} client) Image(client, margin + 7, y + 7, left - 14, headerHeight - 14);
            var titleWidth = width - left - right;
            gfx.DrawRectangle(grid, XBrushes.White, margin + left, y, titleWidth, headerHeight);
            var titleLines = Wrap("EX HAZARDOUS AREA EQUIPMENT DEFECT REGISTER", heading, titleWidth - 16);
            for (var i = 0; i < titleLines.Length; i++) gfx.DrawString(titleLines[i], heading, XBrushes.Black, new XRect(margin + left + 4, y + 18 + i * 16, titleWidth - 8, 16), XStringFormats.TopCenter);
            var rightX = margin + width - right;
            gfx.DrawRectangle(grid, XBrushes.White, rightX, y, logoWidth, headerHeight);
            if (media.CompanyLogo is {} company) Image(company, rightX + 5, y + 5, logoWidth - 10, headerHeight - 10);
            var infoWidth = right - logoWidth;
            var info = new[] { $"Ex-Surveyor / Inspector: {Value(report.Inspectors)}", $"Vessel / Site: {Value(report.SiteName)}", $"Location: {Value(report.Location)}" };
            // Long personnel/site values remain readable in wrapped metadata lines.
            var metadataFont = new XFont("Report Sans", 7.5);
            for (var i = 0; i < info.Length; i++)
            {
                gfx.DrawRectangle(grid, XBrushes.White, rightX + logoWidth, y + i * 24, infoWidth, 24);
                var lines = Wrap(info[i], metadataFont, infoWidth - 8);
                // Full overflow metadata is printed below the band.
                for (var j = 0; j < Math.Min(2, lines.Length); j++) gfx.DrawString(lines[j], metadataFont, XBrushes.Black, new XRect(rightX + logoWidth + 4, y + i * 24 + 3 + j * 10, infoWidth - 8, 10), XStringFormats.TopLeft);
            }
            y += headerHeight;
            var longMetadata = info.Where(s => Wrap(s, metadataFont, infoWidth - 8).Length > 2).ToArray();
            foreach (var value in longMetadata) { var lines = Wrap(value, metadataFont, width - 8); Cell(margin, width, lines.Length * lineHeight + 8, lines, metadataFont, XBrushes.White); y += lines.Length * lineHeight + 8; }
            var headers = Columns.Select((c, i) => Wrap(c, bold, widths[i] - 8)).ToArray(); var height = headers.Max(l => l.Length) * lineHeight + 10;
            var x = margin; for (var i = 0; i < Columns.Length; i++) { Cell(x, widths[i], height, headers[i], bold, blue, true); x += widths[i]; } y += height; bodyStart = y;
        }
        try
        {
            Page();
            foreach (var row in report.Items)
            {
                var cells = Cells(row);
                for (var slot = 1; slot <= 2; slot++) cells[11 + slot] = string.Join("\n", row.Photos.Where(p => p.Slot == slot && !media.Images.ContainsKey(p.Id)).Select(p => "Attachment: " + p.FileName));
                var lines = cells.Select((v, i) => Wrap(v, font, widths[i] - 8)).ToArray();
                var pictures = new[] { row.Photos.Where(p => p.Slot == 1 && media.Images.ContainsKey(p.Id)).ToArray(), row.Photos.Where(p => p.Slot == 2 && media.Images.ContainsKey(p.Id)).ToArray() };
                var textOffset = 0; var pictureOffset = 0; var continuation = false;
                do
                {
                    var fullHeight = Math.Max(30, Math.Max((lines.Max(l => l.Length) - textOffset) * lineHeight + 8, (pictures.Max(p => p.Length) - pictureOffset) * 92 + 8));
                    if (y + fullHeight > bottom && y > bodyStart + 1) Page();
                    var capacity = Math.Max(1, (int)((bottom - y - 8) / lineHeight));
                    var pictureCapacity = Math.Max(1, (int)((bottom - y - 8) / 92));
                    var chunk = lines.Select(l => l.Skip(textOffset).Take(capacity).ToArray()).ToArray();
                    if (continuation && chunk[0].Length == 0) chunk[0] = Wrap(row.ActionNo + " (continued)", font, widths[0] - 8);
                    var photoCount = Math.Max(0, Math.Min(pictureCapacity, pictures.Max(p => p.Length) - pictureOffset));
                    var height = Math.Max(30, Math.Max(chunk.Max(l => l.Length) * lineHeight + 8, photoCount * 92 + 8));
                    var x = margin;
                    for (var i = 0; i < 16; i++)
                    {
                        var color = i is 2 or 4 ? XColor.FromArgb(255, 242, 153) : i == 11 ? XColor.FromArgb(Convert.ToInt32("FF" + StatusColor(row.Status), 16)) : XColors.White;
                        Cell(x, widths[i], height, chunk[i], font, new XSolidBrush(color));
                        if (i is 12 or 13)
                            foreach (var pair in pictures[i - 12].Skip(pictureOffset).Take(pictureCapacity).Select((p, n) => (p, n))) Image(media.Images[pair.p.Id], x + 4, y + 4 + pair.n * 92, widths[i] - 8, 84);
                        x += widths[i];
                    }
                    y += height; textOffset += capacity; pictureOffset += pictureCapacity; continuation = true;
                } while (textOffset < lines.Max(l => l.Length) || pictureOffset < pictures.Max(p => p.Length));
            }
            if (report.Items.Count == 0) Cell(margin, widths.Sum(), 30, ["No defects recorded for this project."], font, XBrushes.White);
        }
        finally { gfx?.Dispose(); foreach (var image in imageCache.Values) image.Dispose(); }
        using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
    }
}
