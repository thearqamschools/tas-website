using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// A report as a plain table (title, columns, rows, totals) that is shown on the page, printed, and
    /// saved as PDF (<see cref="SimplePdf"/>) or Excel (.xlsx written here, no outside library).
    /// Money and counts are kept as numbers so Excel can add them up.
    /// </summary>
    public class ReportTable
    {
        public class Column
        {
            public string Name;
            /// <summary>"text", "money" or "number".</summary>
            public string Kind = "text";
            /// <summary>Relative width in the PDF.</summary>
            public double Width = 1;
        }

        public string Title;
        public string Subtitle;
        public List<Column> Columns = new List<Column>();
        public List<object[]> Rows = new List<object[]>();
        public object[] Totals;
        public List<string> Notes = new List<string>();

        public ReportTable Col(string name, string kind = "text", double width = 1)
        {
            Columns.Add(new Column { Name = name, Kind = kind, Width = width });
            return this;
        }

        public static string Format(object v, string kind)
        {
            if (v == null) return "";
            if (kind == "money" && v is decimal) return ((decimal)v).ToString("#,##0.##", CultureInfo.InvariantCulture);
            if (kind == "number" && v is int) return ((int)v).ToString("#,##0", CultureInfo.InvariantCulture);
            if (kind == "number" && v is decimal) return ((decimal)v).ToString("0.##", CultureInfo.InvariantCulture);
            if (v is decimal) return ((decimal)v).ToString("#,##0.##", CultureInfo.InvariantCulture);
            if (v is DateTime) return ((DateTime)v).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        // ---- PDF ------------------------------------------------------------------------------

        public byte[] ToPdf()
        {
            var pdf = new SimplePdf { Title = Title };
            const double left = 30, right = 565;
            var total = Columns.Sum(c => c.Width);
            var widths = Columns.Select(c => c.Width / total * (right - left)).ToArray();
            var top = 0.0;
            var page = 0;
            Action head = () =>
            {
                pdf.NewPage();
                page++;
                top = 30;
                pdf.Text(left, top, 14, "The Arqam Schools \u2013 " + Title, true, "left", 0.15);
                if (!string.IsNullOrEmpty(Subtitle)) pdf.Text(left, top + 18, 9, Subtitle, false, "left", 0.35);
                pdf.Text(right, top + 2, 8, "Page " + page, false, "right", 0.45);
                top += 36;
                pdf.Box(left, top, right - left, 16, 0.93, 0.94, 0.98);
                var x = left;
                for (var i = 0; i < Columns.Count; i++)
                {
                    var numeric = Columns[i].Kind != "text";
                    pdf.TextFit(numeric ? x + widths[i] - 3 : x + 3, top + 4, 7.5, Columns[i].Name.ToUpperInvariant(), widths[i] - 6, true, numeric ? "right" : "left");
                    x += widths[i];
                }
                top += 20;
            };
            Action<object[], bool> row = (cells, bold) =>
            {
                if (top > 800) head();
                var x = left;
                for (var i = 0; i < Columns.Count; i++)
                {
                    var numeric = Columns[i].Kind != "text";
                    var text = i < cells.Length ? Format(cells[i], Columns[i].Kind) : "";
                    pdf.TextFit(numeric ? x + widths[i] - 3 : x + 3, top, 8, text, widths[i] - 6, bold, numeric ? "right" : "left");
                    x += widths[i];
                }
                top += 12;
                pdf.Line(left, top - 2.5, right, top - 2.5, 0.3, 0.88);
            };
            head();
            if (Rows.Count == 0) { pdf.Text(left + 3, top, 9, "Nothing to show for this period."); top += 14; }
            foreach (var r in Rows) row(r, false);
            if (Totals != null) { top += 2; row(Totals, true); }
            foreach (var n in Notes) { if (top > 810) head(); top += 4; pdf.Text(left, top, 7.5, n, false, "left", 0.4); top += 10; }
            pdf.Text(left, 820, 7, "Printed " + DateTime.UtcNow.AddHours(5).ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture) + " (Pakistan time)", false, "left", 0.5);
            return pdf.ToBytes();
        }

        // ---- Excel (.xlsx) ---------------------------------------------------------------------

        /// <summary>A one-sheet .xlsx: title row, header row (bold), data rows, totals row (bold). Numbers stay numbers.</summary>
        public byte[] ToXlsx()
        {
            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
                 .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols>");
            for (var i = 0; i < Columns.Count; i++)
                sheet.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1).Append("\" width=\"")
                     .Append((Columns[i].Kind == "text" ? 14 + Columns[i].Width * 8 : 14).ToString("0", CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
            sheet.Append("</cols><sheetData>");
            var r = 1;
            Action<IEnumerable<object>, int, string[]> write = (cells, style, kinds) =>
            {
                sheet.Append("<row r=\"").Append(r).Append("\">");
                var c = 0;
                foreach (var v in cells)
                {
                    var kind = kinds != null && c < kinds.Length ? kinds[c] : "text";
                    var refName = ColumnName(c) + r;
                    if (v == null) { c++; continue; }
                    var number = (v is decimal || v is int || v is long || v is double) && kind != "text";
                    if (number)
                        sheet.Append("<c r=\"").Append(refName).Append("\"").Append(style > 0 ? " s=\"" + (style == 1 ? 3 : 2) + "\"" : " s=\"2\"")
                             .Append("><v>").Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append("</v></c>");
                    else
                        sheet.Append("<c r=\"").Append(refName).Append("\" t=\"inlineStr\"").Append(style > 0 ? " s=\"1\"" : "")
                             .Append("><is><t xml:space=\"preserve\">").Append(SecurityElement.Escape(XmlSafe(Format(v, kind)))).Append("</t></is></c>");
                    c++;
                }
                sheet.Append("</row>");
                r++;
            };
            var kindsArr = Columns.Select(x => x.Kind).ToArray();
            write(new object[] { "The Arqam Schools \u2013 " + Title }, 1, null);
            if (!string.IsNullOrEmpty(Subtitle)) write(new object[] { Subtitle }, 0, null);
            r++;
            write(Columns.Select(x => (object)x.Name), 1, null);
            foreach (var row in Rows) write(row, 0, kindsArr);
            if (Totals != null) write(Totals, 1, kindsArr);
            foreach (var n in Notes) write(new object[] { n }, 0, null);
            sheet.Append("</sheetData></worksheet>");

            var sheetName = SecurityElement.Escape(new string((Title ?? "Report").Where(ch => "[]:*?/\\".IndexOf(ch) < 0).Take(31).ToArray()));
            var files = new Dictionary<string, string>
            {
                { "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>" },
                { "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>" },
                { "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + (sheetName.Length == 0 ? "Report" : sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>" },
                { "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>" },
                // Styles: 0 normal, 1 bold text, 2 number #,##0.##, 3 bold number.
                { "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>" },
                { "xl/worksheets/sheet1.xml", sheet.ToString() }
            };
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var f in files)
                    {
                        var entry = zip.CreateEntry(f.Key, CompressionLevel.Optimal);
                        using (var w = new StreamWriter(entry.Open(), new UTF8Encoding(false))) w.Write(f.Value);
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>XML 1.0 does not allow most control characters; one in a name or note would make the file unreadable.</summary>
        private static string XmlSafe(string s)
        {
            return new string((s ?? "").Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ').ToArray());
        }

        private static string ColumnName(int index)
        {
            var name = "";
            index++;
            while (index > 0) { var m = (index - 1) % 26; name = (char)('A' + m) + name; index = (index - 1) / 26; }
            return name;
        }
    }
}
