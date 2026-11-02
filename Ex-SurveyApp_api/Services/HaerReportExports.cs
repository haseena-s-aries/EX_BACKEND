using System.Text;
using System.Xml.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Ex_SurveyApp_api.Services;

public static class HaerReportExports
{
    private static readonly string[] Columns = ["Site", "Area", "Item Number", "Equipment Tag", "Equipment ID", "Description", "Equipment Type", "Location", "Manufacturer", "Model", "Serial Number", "Ex Protection Method", "Gas Group", "Temperature Class", "Hazardous Area Zone", "Ambient Temperature Range", "IP Rating", "Ex Certificate Number", "Notified Body", "Special Conditions of Use", "Circuit Reference", "RFID UID", "RFID Identification Method", "Latest Inspection Grade", "Latest Inspection Date", "Latest Inspection Result", "Latest Review Status", "Outstanding Defects", "Defect Statuses", "Equipment Inspection History", "Reconciliation Status"];

    private static string Value(string key, string? value, HaerRow row) => row.MetadataAvailability.TryGetValue(key, out var state) && state == "NotApplicable" ? "Not Applicable" : string.IsNullOrWhiteSpace(value) ? "Unknown / not recorded" : value;
    private static string[] Cells(HaerReport report, HaerRow e) => [report.SiteName, e.Area, e.SequenceNo.ToString(), e.TagNumber, e.EquipmentId,
        Value("description", e.Description, e), e.EquipmentType, Value("location", e.Location, e), Value("manufacturer", e.Manufacturer, e), Value("model", e.Model, e),
        Value("serialNumber", e.SerialNumber, e), Value("exProtectionType", e.ExProtectionType, e), Value("gasGroup", e.GasGroup, e), Value("temperatureClass", e.TemperatureClass, e),
        Value("hazardousAreaZone", e.HazardousAreaZone, e), Value("ambientTemperatureRange", e.AmbientTemperatureRange, e), Value("ipRating", e.IpRating, e),
        Value("certificateNumber", e.CertificateNumber, e), Value("notifiedBody", e.NotifiedBody, e), Value("specialConditionsOfUse", e.SpecialConditionsOfUse, e),
        Value("circuitId", e.CircuitId, e), Value("rfidTagUid", e.RfidTagUid, e), Value("rfidIdentificationMethod", e.RfidIdentificationMethod, e),
        e.LastInspectionGrade ?? "Unknown / not recorded", e.LastInspectionAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "Unknown / not recorded",
        e.LastInspectionResult ?? "Unknown / not recorded", e.QcStatus ?? e.InspectionStatus, e.OutstandingDefects.ToString(), string.Join("; ", e.OutstandingDefectStatuses),
        string.Join("; ", e.InspectionHistory.Select(h => $"{h.Grade} {h.InspectedAt?.ToUniversalTime():yyyy-MM-dd} {h.Status} {h.Result}")), e.ReconciliationStatus];

    public static byte[] Excel(HaerReport report)
    {
        XNamespace ss = "urn:schemas-microsoft-com:office:spreadsheet";
        XNamespace x = "urn:schemas-microsoft-com:office:excel";
        var rows = new List<XElement> {
            new XElement(ss + "Row", new XElement(ss + "Cell", new XAttribute(ss + "StyleID", "Header"), new XElement(ss + "Data", new XAttribute(ss + "Type", "String"), "HAER — " + report.ProjectName))),
            new XElement(ss + "Row", new XElement(ss + "Cell", new XElement(ss + "Data", new XAttribute(ss + "Type", "String"), $"Job: {report.JobNumber} · Client: {report.ClientName ?? "Unknown / not recorded"} · Revision: {report.Revision ?? "Unspecified"} · Generated UTC: {report.GeneratedAt:O}"))),
            new XElement(ss + "Row", Columns.Select(c => new XElement(ss + "Cell", new XAttribute(ss + "StyleID", "Header"), new XElement(ss + "Data", new XAttribute(ss + "Type", "String"), c))))
        };
        rows.AddRange(report.Equipment.Select(e => new XElement(ss + "Row", Cells(report, e).Select(value => new XElement(ss + "Cell", new XElement(ss + "Data", new XAttribute(ss + "Type", "String"), value ?? ""))))));
        var workbook = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(ss + "Workbook",
            new XAttribute(XNamespace.Xmlns + "ss", ss), new XAttribute(XNamespace.Xmlns + "x", x),
            new XElement(ss + "Styles", new XElement(ss + "Style", new XAttribute(ss + "ID", "Header"), new XElement(ss + "Font", new XAttribute(ss + "Bold", "1")))),
            new XElement(ss + "Worksheet", new XAttribute(ss + "Name", "HAER"), new XElement(ss + "Table", rows))));
        using var stream = new MemoryStream(); using (var writer = System.Xml.XmlWriter.Create(stream, new System.Xml.XmlWriterSettings {Encoding = new UTF8Encoding(true), Indent = true})) workbook.Save(writer);
        return stream.ToArray();
    }

    public static byte[] Pdf(HaerReport report)
    {
        ProjectReportPdf.EnsureFonts();
        using var pdf = new PdfDocument(); pdf.Info.Title = $"HAER - {report.ProjectName}"; pdf.Info.Creator = "Ex-Survey"; pdf.Info.Subject = "Hazardous Area Equipment Register";
        PdfPage page = null!; XGraphics gfx = null!; XFont title = new("Report Sans", 16, XFontStyleEx.Bold); XFont header = new("Report Sans", 9, XFontStyleEx.Bold); XFont font = new("Report Sans", 8); double y = 0;
        void NewPage()
        {
            gfx?.Dispose(); page = pdf.AddPage(); page.Size = PageSize.A4; page.Orientation = PageOrientation.Landscape; gfx = XGraphics.FromPdfPage(page); y = 28;
            gfx.DrawString("HAZARDOUS AREA EQUIPMENT REGISTER", title, XBrushes.Black, 28, y + 16); y += 22;
            gfx.DrawString($"{report.ProjectName} · Job {report.JobNumber} · Site {report.SiteName}", header, XBrushes.Black, 28, y); y += 13;
            gfx.DrawString($"Client: {report.ClientName ?? "Unknown / not recorded"} · Revision: {report.Revision ?? "Unspecified"} · Generated UTC: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}", font, XBrushes.Black, 28, y); y += 18;
        }
        NewPage();
        foreach (var row in report.Equipment)
        {
            if (y > page.Height.Point - 105) NewPage();
            var vals = Cells(report, row);
            gfx.DrawString($"Item {row.SequenceNo} · {row.TagNumber} · {row.EquipmentType} · {row.Area}", header, XBrushes.DarkBlue, 28, y); y += 13;
            for (var i = 0; i < Columns.Length; i++)
            {
                var text = $"{Columns[i]}: {vals[i]}";
                foreach (var line in Wrap(text, 150)) { if (y > page.Height.Point - 32) NewPage(); gfx.DrawString(line, font, XBrushes.Black, 34, y); y += 10; }
            }
            y += 6;
        }
        if (report.Equipment.Count == 0) gfx.DrawString("No equipment rows match this register filter.", font, XBrushes.Black, 28, y);
        gfx.Dispose(); using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var remaining = text;
        while (remaining.Length > width) {var split = remaining.LastIndexOf(' ', width); if (split < 1) split = width; yield return remaining[..split]; remaining = remaining[(split + 1)..];}
        yield return remaining;
    }
}
