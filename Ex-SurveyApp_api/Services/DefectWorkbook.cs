using System.IO.Compression;
using System.Xml.Linq;

namespace Ex_SurveyApp_api.Services;

internal static class DefectWorkbook
{
    public static byte[] Export(DefectRegisterReport report, DefectReportMedia media)
    {
        var columns = DefectReportExports.Columns.Select((label, i) => (Label: label, Left: i >= 8 || i == 6)).ToArray();
        bool CanEmbed(DefectReportRow row, DefectReportPhoto photo) => media.Images.ContainsKey(photo.Id)
            && row.Photos.Count(p => p.Slot == photo.Slot && media.Images.ContainsKey(p.Id)) <= 3;
        var rows = report.Items.Select(DefectReportExports.Cells).ToArray();
        for (var r = 0; r < rows.Length; r++)
            for (var slot = 1; slot <= 2; slot++) rows[r][11 + slot] = string.Join("\n", report.Items[r].Photos.Where(p => p.Slot == slot && !CanEmbed(report.Items[r], p)).Select(p => "Attachment (not embedded): " + p.FileName + "\n" + p.Url));
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace officeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var widths = columns.Select((c, i) => Math.Clamp(rows.Select(r => r[i]).Append(c.Label)
            .SelectMany(v => v.Split('\n')).Max(v => v.Length) + 2d, i is 12 or 13 ? 23 : i is 8 or 9 or 10 or 14 or 15 ? 28 : 14, c.Left ? 55 : 27)).ToArray();
        XElement Alignment(string horizontal) => new(n + "alignment", new XAttribute("horizontal", horizontal), new XAttribute("vertical", "top"), new XAttribute("wrapText", 1));
        XElement Style(int font, int fill, string horizontal) => new(n + "xf", new XAttribute("numFmtId", 49), new XAttribute("fontId", font), new XAttribute("fillId", fill), new XAttribute("borderId", 1), new XAttribute("xfId", 0), new XAttribute("applyNumberFormat", 1), new XAttribute("applyAlignment", 1), Alignment(horizontal));
        var styles = new XElement(n + "styleSheet",
            new XElement(n + "fonts", new XAttribute("count", 2), Enumerable.Range(0, 2).Select(i => new XElement(n + "font", i == 1 ? new XElement(n + "b") : null, new XElement(n + "sz", new XAttribute("val", 11)), new XElement(n + "name", new XAttribute("val", "Calibri"))))),
            new XElement(n + "fills", new XAttribute("count", 7), new XElement(n + "fill", new XElement(n + "patternFill", new XAttribute("patternType", "none"))), new XElement(n + "fill", new XElement(n + "patternFill", new XAttribute("patternType", "gray125"))), new XElement(n + "fill", new XElement(n + "patternFill", new XAttribute("patternType", "solid"), new XElement(n + "fgColor", new XAttribute("rgb", "FFD3E1EE")), new XElement(n + "bgColor", new XAttribute("indexed", 64)))), new[] { "FFFFF299", "FFC6EFCE", "FFFFC7CE", "FFFFEB9C" }.Select(color => new XElement(n + "fill", new XElement(n + "patternFill", new XAttribute("patternType", "solid"), new XElement(n + "fgColor", new XAttribute("rgb", color)))))),
            new XElement(n + "borders", new XAttribute("count", 2), new XElement(n + "border"), new XElement(n + "border", new[] { "left", "right", "top", "bottom" }.Select(side => new XElement(n + side, new XAttribute("style", "thin"), new XElement(n + "color", new XAttribute("rgb", "FF000000")))))),
            new XElement(n + "cellStyleXfs", new XAttribute("count", 1), new XElement(n + "xf", new XAttribute("numFmtId", 0), new XAttribute("fontId", 0), new XAttribute("fillId", 0), new XAttribute("borderId", 0))),
            new XElement(n + "cellXfs", new XAttribute("count", 7), Style(1, 2, "center"), Style(0, 0, "left"), Style(0, 0, "center"), Style(0, 3, "left"), Style(0, 4, "left"), Style(0, 5, "left"), Style(0, 6, "left")),
            new XElement(n + "cellStyles", new XAttribute("count", 1), new XElement(n + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));
        var allRows = new[] { columns.Select(c => c.Label).ToArray() }.Concat(rows).ToArray();
        var sheet = new XElement(n + "worksheet",
            new XElement(n + "dimension", new XAttribute("ref", $"A1:P{allRows.Length}")),
            new XElement(n + "sheetViews", new XElement(n + "sheetView", new XAttribute("workbookViewId", 0), new XElement(n + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
            new XElement(n + "sheetFormatPr", new XAttribute("defaultRowHeight", 15)),
            new XElement(n + "cols", widths.Select((width, i) => new XElement(n + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", width), new XAttribute("customWidth", 1)))),
            new XElement(n + "sheetData", allRows.Select((row, index) => new XElement(n + "row", new XAttribute("r", index + 1), new XAttribute("ht", index == 0 ? 36 : Math.Clamp(row.Select((v, i) => v.Split('\n').Sum(line => Math.Max(1, Math.Ceiling(line.Length / (widths[i] - 2))))).Max() * 15 + 6, index > 0 ? Math.Min(409, Math.Max(30, report.Items[index - 1].Photos.Where(p => CanEmbed(report.Items[index - 1], p)).GroupBy(p => p.Slot).Select(g => g.Count()).DefaultIfEmpty().Max() * 95)) : 30, 409)), new XAttribute("customHeight", 1), row.Select((value, i) => new XElement(n + "c", new XAttribute("r", $"{(char)('A' + i)}{index + 1}"), new XAttribute("s", index == 0 ? 0 : i is 2 or 4 ? 3 : i == 11 ? DefectReportExports.StatusColor(report.Items[index - 1].Status) switch { "C6EFCE" => 4, "FFC7CE" => 5, "FFEB9C" => 6, _ => 1 } : columns[i].Left ? 1 : 2), new XAttribute("t", "inlineStr"), new XElement(n + "is", new XElement(n + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))))))),
            new XElement(n + "autoFilter", new XAttribute("ref", $"A1:P{allRows.Length}")),
            new XElement(n + "pageMargins", new XAttribute("left", .25), new XAttribute("right", .25), new XAttribute("top", .5), new XAttribute("bottom", .5), new XAttribute("header", .2), new XAttribute("footer", .2)),
            new XElement(n + "pageSetup", new XAttribute("paperSize", 8), new XAttribute("orientation", "landscape")));
        var images = report.Items.SelectMany((row, r) => row.Photos.Where(p => CanEmbed(row, p)).Select(p => new { Row = r + 1, Photo = p })).ToArray();
        if (images.Length > 0) sheet.Add(new XElement(n + "drawing", new XAttribute(officeRel + "id", "rId1")));
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Write(string path, XElement xml) { using var stream = zip.CreateEntry(path).Open(); new XDocument(xml).Save(stream); }
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            Write("[Content_Types].xml", new XElement(ct + "Types", new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement(ct + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")), images.Length > 0 ? new XElement(ct + "Override", new XAttribute("PartName", "/xl/drawings/drawing1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.drawing+xml")) : null, new[] { ("workbook", "sheet.main"), ("styles", "styles"), ("worksheets/sheet1", "worksheet") }.Select(p => new XElement(ct + "Override", new XAttribute("PartName", $"/xl/{p.Item1}.xml"), new XAttribute("ContentType", $"application/vnd.openxmlformats-officedocument.spreadsheetml.{p.Item2}+xml")))));
            Write("_rels/.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", officeRel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
            Write("xl/workbook.xml", new XElement(n + "workbook", new XElement(n + "sheets", new XElement(n + "sheet", new XAttribute("name", "Defect Register"), new XAttribute("sheetId", 1), new XAttribute(officeRel + "id", "rId1")))));
            Write("xl/_rels/workbook.xml.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", officeRel.NamespaceName + "/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")), new XElement(rel + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Type", officeRel.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml"))));
            Write("xl/styles.xml", styles);
            Write("xl/worksheets/sheet1.xml", sheet);
            if (images.Length > 0)
            {
                XNamespace xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                var anchors = new List<XElement>(); var imageRels = new XElement(rel + "Relationships");
                var slotOffsets = new Dictionary<(int Row, int Slot), int>();
                for (var i = 0; i < images.Length; i++)
                {
                    var entry = images[i]; var picture = media.Images[entry.Photo.Id];
                    var key = (entry.Row, entry.Photo.Slot); var offset = slotOffsets.GetValueOrDefault(key); slotOffsets[key] = offset + 1;
                    var width = widths[11 + entry.Photo.Slot] * 7 - 12;
                    var scale = Math.Min(width / picture.Width, 110d / picture.Height);
                    var cx = (long)(picture.Width * scale * 9525); var cy = (long)(picture.Height * scale * 9525);
                    imageRels.Add(new XElement(rel + "Relationship", new XAttribute("Id", $"rId{i + 1}"), new XAttribute("Type", officeRel.NamespaceName + "/image"), new XAttribute("Target", $"../media/image{i + 1}.png")));
                    using (var imageStream = zip.CreateEntry($"xl/media/image{i + 1}.png").Open()) imageStream.Write(picture.Png);
                    anchors.Add(new XElement(xdr + "oneCellAnchor",
                        new XElement(xdr + "from", new XElement(xdr + "col", 11 + entry.Photo.Slot), new XElement(xdr + "colOff", 6 * 9525), new XElement(xdr + "row", entry.Row), new XElement(xdr + "rowOff", (6 + offset * 126) * 9525)),
                        new XElement(xdr + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                        new XElement(xdr + "pic", new XElement(xdr + "nvPicPr", new XElement(xdr + "cNvPr", new XAttribute("id", i + 1), new XAttribute("name", entry.Photo.FileName)), new XElement(xdr + "cNvPicPr", new XElement(a + "picLocks", new XAttribute("noChangeAspect", 1)))),
                            new XElement(xdr + "blipFill", new XElement(a + "blip", new XAttribute(officeRel + "embed", $"rId{i + 1}")), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                            new XElement(xdr + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))), new XElement(xdr + "clientData")));
                }
                Write("xl/drawings/drawing1.xml", new XElement(xdr + "wsDr", anchors));
                Write("xl/drawings/_rels/drawing1.xml.rels", imageRels);
                Write("xl/worksheets/_rels/sheet1.xml.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", officeRel.NamespaceName + "/drawing"), new XAttribute("Target", "../drawings/drawing1.xml"))));
            }
        }
        return output.ToArray();
    }
}
