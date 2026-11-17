using System.Text;
using SkiaSharp;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Ex_SurveyApp_api.Services;

public static class HaerReportExports
{
    public static byte[] Csv(HaerReport report)
    {
        static string Quote(string value)
        {
            // Keep spreadsheet formula protection while retaining commas, quotes and line breaks.
            var first = value.AsSpan().TrimStart();
            if (!first.IsEmpty && first[0] is '=' or '+' or '-' or '@') value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var rows = new[] {SpreadsheetColumns.Select(column => column.Label).ToArray()}
            .Concat(report.Equipment.Select((row, index) => SpreadsheetCells(report, row, index + 1)));
        var csv = string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Quote)))) + "\r\n";
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    public static byte[] Excel(HaerReport report) => HaerWorkbook.Export(
        SpreadsheetColumns,
        report.Equipment.Select((row, index) => SpreadsheetCells(report, row, index + 1)).ToArray());
    private static (string Label, bool Left)[] SpreadsheetColumns =>
        RegisterColumns.Take(1).Select(column => (column.Label, column.Left))
            .Append(("Project Name", true))
            .Concat(RegisterColumns.Skip(1).Select(column => (column.Label, column.Left))).ToArray();

    private static string[] SpreadsheetCells(HaerReport report, HaerRow row, int number)
    {
        var cells = RegisterCells(row, number, preserveText: true);
        return [cells[0], report.ProjectName, .. cells.Skip(1)];
    }
    private static readonly (string Label, double Width, bool Left)[] RegisterColumns =
    [
        ("Sl. No", 36, false), ("Item #", 49, false), ("Ex-Tag", 52, false), ("Area", 59, false),
        ("Description", 108, true), ("Manufacturer", 78, true), ("Type/Model", 74, true),
        ("Serial No", 57, false), ("Zone/Division", 53, false), ("Protection", 55, false),
        ("Gas Group", 45, false), ("Temp Class", 45, false), ("Amb Temp", 54, false),
        ("IP/NEMA Code", 54, false), ("Notified Body", 59, false), ("Cert/EC #", 68, false),
        ("Special Conditions", 62, true), ("Type of Inspection V, C, D", 57, false),
        ("Date of Last Inspection", 63, false), ("Comments", 121, true), ("Status", 64, false)
    ];

    public static byte[] Pdf(HaerReport report, string? location = null, string? companyLogoName = null, Stream? clientLogoStream = null)
    {
        ProjectReportPdf.EnsureFonts();
        using var pdf = new PdfDocument();
        pdf.Info.Title = $"HAER - {report.ProjectName}";
        pdf.Info.Creator = "Ex-Survey";
        pdf.Info.Subject = "Hazardous Area Equipment Register";
        using var companyLogoStream = CompanyAssets.Logo(companyLogoName) ?? CompanyAssets.Logo("aries_mar_engg.png");
        
        using var companyLogo = companyLogoStream is null ? null : XImage.FromStream(companyLogoStream);
        using var clientLogoBuffer = new MemoryStream();
        if (clientLogoStream is not null) clientLogoStream.CopyTo(clientLogoBuffer);
        using var clientLogo = clientLogoStream is null ? null : LoadLogo(clientLogoBuffer);

        var inspectors = report.Equipment.Select(row => row.Inspector?.Trim())
            .Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var inspector = inspectors.Length switch { 0 => "—", 1 => inspectors[0], _ => "Multiple inspectors" };

        const double margin = 25, bandHeight = 49, headingHeight = 32, cellPadding = 3;
        var titleFont = new XFont("Report Sans", 12, XFontStyleEx.Bold);
        var headerFont = new XFont("Report Sans", 7.5, XFontStyleEx.Bold);
        var bodyFont = new XFont("Report Sans", 7.5);
        var grid = new XPen(XColors.Black, 0.45);
        var blue = new XSolidBrush(XColor.FromArgb(156, 194, 226));
        var totalWeight = RegisterColumns.Sum(column => column.Width);
        XGraphics? graphics = null;
        double[] widths = [];
        double y = 0, bottom = 0;

        void Logo(XImage? image, double x, double width)
        {
            if (image is null) return;
            var scale = Math.Min((width - 16) / image.PixelWidth, (bandHeight - 12) / image.PixelHeight);
            var imageWidth = image.PixelWidth * scale;
            var imageHeight = image.PixelHeight * scale;
            graphics!.DrawImage(image, x + (width - imageWidth) / 2, y + (bandHeight - imageHeight) / 2, imageWidth, imageHeight);
        }

        void DrawCell(double x, double width, double height, XFont font, IReadOnlyList<string> lines, bool left, XBrush background)
        {
            graphics!.DrawRectangle(grid, background, x, y, width, height);
            var lineHeight = font.Size * 1.22;
            var top = y + (height - lines.Count * lineHeight) / 2;
            for (var line = 0; line < lines.Count; line++)
                graphics.DrawString(lines[line], font, XBrushes.Black,
                    new XRect(x + cellPadding, top + line * lineHeight, width - 2 * cellPadding, lineHeight),
                    left ? XStringFormats.CenterLeft : XStringFormats.Center);
        }

        void StartPage()
        {
            graphics?.Dispose();
            var page = pdf.AddPage();
            page.Size = PageSize.A3;
            page.Orientation = PageOrientation.Landscape;
            graphics = XGraphics.FromPdfPage(page);
            y = margin;
            bottom = page.Height.Point - margin;
            var width = page.Width.Point - margin * 2;
            widths = RegisterColumns.Select(column => width * column.Width / totalWeight).ToArray();

            var companyWidth = width * .094;
            var ariesWidth = width * .075;
            var metadataWidth = width * .225;
            var titleWidth = width - companyWidth - ariesWidth - metadataWidth;
            var titleX = margin + companyWidth;
            var ariesX = titleX + titleWidth;
            var metadataX = ariesX + ariesWidth;
            DrawCell(margin, companyWidth, bandHeight, headerFont, [], false, XBrushes.White);
            DrawCell(titleX, titleWidth, bandHeight, titleFont, ["HAZARDOUS AREA EQUIPMENT REGISTER"], false, XBrushes.White);
            DrawCell(ariesX, ariesWidth, bandHeight, headerFont, [], false, XBrushes.White);
            Logo(clientLogo, margin, companyWidth);
            Logo(companyLogo, ariesX, ariesWidth);

            foreach (var label in new[] { $"Vessel/site : {Display(report.SiteName)}", $"Location : {Display(location)}", $"Inspector : {inspector}" })
            {
                var size = headerFont.Size;
                var labelWidth = graphics.MeasureString(label, headerFont).Width;
                if (labelWidth > metadataWidth - 2 * cellPadding)
                    size *= (metadataWidth - 2 * cellPadding) / labelWidth;
                var metadataFont = size == headerFont.Size ? headerFont : new XFont("Report Sans", size, XFontStyleEx.Bold);
                DrawCell(metadataX, metadataWidth, bandHeight / 3, metadataFont, [label], false, XBrushes.White);
                y += bandHeight / 3;
            }
            var x = margin;
            for (var column = 0; column < RegisterColumns.Length; column++)
            {
                DrawCell(x, widths[column], headingHeight, headerFont,
                    Wrap(graphics, RegisterColumns[column].Label, headerFont, widths[column] - 2 * cellPadding), false, blue);
                x += widths[column];
            }
            y += headingHeight;
        }

        try
        {
            StartPage();
            for (var index = 0; index < report.Equipment.Count; index++)
            {
                var cells = RegisterCells(report.Equipment[index], index + 1);
                var font = bodyFont;
                var maximumHeight = bottom - margin - bandHeight - headingHeight;
                string[][] lines;
                double height;
                while (true)
                {
                    lines = cells.Select((cell, col) => Wrap(graphics!, cell, font, widths[col] - 2 * cellPadding)).ToArray();
                    height = Math.Max(23, lines.Max(cell => cell.Length) * font.Size * 1.22 + 2 * cellPadding);
                    if (height <= maximumHeight) break;
                    font = new XFont("Report Sans", font.Size * .9);
                }
                if (y + height > bottom) StartPage();
                var x = margin;
                for (var column = 0; column < cells.Length; column++)
                {
                    DrawCell(x, widths[column], height, font, lines[column], RegisterColumns[column].Left, XBrushes.White);
                    x += widths[column];
                }
                y += height;
            }
            if (report.Equipment.Count == 0)
                DrawCell(margin, widths.Sum(), 25, bodyFont, ["No equipment rows match this register filter."], false, XBrushes.White);
        }
        finally { graphics?.Dispose(); }
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static XImage LoadLogo(MemoryStream source)
    {
        source.Position = 0;
        try { return XImage.FromStream(source); }
        catch (InvalidOperationException)
        {
            // Normalize image variants (for example grayscale PNG) unsupported by PDFsharp.
            using var bitmap = SKBitmap.Decode(source.ToArray()) ?? throw new InvalidDataException("Unreadable client logo.");
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return XImage.FromStream(new MemoryStream(png.ToArray()));
        }
    }

    private static string[] RegisterCells(HaerRow row, int number, bool preserveText = false)
    {
        string Text(string? value) => preserveText && !string.IsNullOrWhiteSpace(value) ? value : Display(value);
        string Field(string key, string? value) =>
            row.MetadataAvailability.GetValueOrDefault(key) == "NotApplicable" ? "N/A" : Text(value);
        var grade = row.LastInspectionGrade switch { "Visual" => "V", "Close" => "C", "Detailed" => "D", _ => Text(row.LastInspectionGrade) };
        var model = Field("model", row.Model);
        return
        [
            row.SequenceNo.ToString(), Text(row.ItemNumber), Text(row.TagNumber), Text(row.Area),
            Field("description", row.Description), Field("manufacturer", row.Manufacturer),
            model == "—" ? Text(row.EquipmentType) : $"{Text(row.EquipmentType)} / {model}",
            Field("serialNumber", row.SerialNumber), Field("hazardousAreaZone", row.HazardousAreaZone),
            Field("exProtectionType", row.ExProtectionType), Field("gasGroup", row.GasGroup),
            Field("temperatureClass", row.TemperatureClass), Field("ambientTemperatureRange", row.AmbientTemperatureRange),
            Field("ipRating", row.IpRating), Field("notifiedBody", row.NotifiedBody), Field("certificateNumber", row.CertificateNumber),
            Field("specialConditionsOfUse", row.SpecialConditionsOfUse), grade,
            row.LastInspectionAt?.ToUniversalTime().ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "—",
            Text(row.Comments), System.Text.RegularExpressions.Regex.Replace(row.InspectionStatus, "([a-z])([A-Z])", "$1 $2")
        ];
    }

    private static string[] Wrap(XGraphics graphics, string text, XFont font, double width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Split('\n'))
        {
            var remaining = paragraph.Trim();
            while (graphics.MeasureString(remaining, font).Width > width && remaining.Length > 1)
            {
                var low = 1;
                var high = remaining.Length - 1;
                while (low < high)
                {
                    var middle = (low + high + 1) / 2;
                    if (graphics.MeasureString(remaining[..middle], font).Width <= width) low = middle;
                    else high = middle - 1;
                }
                var space = remaining.LastIndexOf(' ', low);
                var separator = Math.Max(remaining.LastIndexOf('-', low - 1), remaining.LastIndexOf('/', low - 1));
                var breakpoint = Math.Max(space, separator + 1);
                var end = breakpoint > 0 ? breakpoint : low;
                if (char.IsHighSurrogate(remaining[end - 1]) && char.IsLowSurrogate(remaining[end]))
                    end = end > 1 ? end - 1 : 2;
                lines.Add(remaining[..end]);
                remaining = remaining[end..].TrimStart();
            }
            lines.Add(remaining);
        }
        return lines.ToArray();
    }
}
