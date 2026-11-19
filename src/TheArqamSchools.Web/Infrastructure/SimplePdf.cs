using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// A small PDF writer for invoices and reports (A4, the standard Helvetica fonts, lines, boxes and one
    /// JPEG picture such as the logo). No outside library is needed, so nothing changes in the hosting.
    /// Positions are in points from the TOP-left corner of the page (595 × 842).
    /// Text uses the PDF WinAnsi character set: English text, digits and common signs. Urdu would need an
    /// embedded font and is not supported here.
    /// </summary>
    public class SimplePdf
    {
        public const double PageWidth = 595.28, PageHeight = 841.89;

        private readonly List<StringBuilder> _pages = new List<StringBuilder>();
        private readonly List<byte[]> _images = new List<byte[]>();
        private readonly List<int[]> _imageSizes = new List<int[]>();
        private StringBuilder _page;
        public string Title { get; set; }

        public int PageCount { get { return _pages.Count; } }

        public void NewPage()
        {
            _page = new StringBuilder();
            _pages.Add(_page);
        }

        private static string N(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        public void Text(double x, double top, double size, string text, bool bold = false, string align = "left", double gray = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            var w = Width(text, size, bold);
            if (align == "right") x -= w;
            else if (align == "center") x -= w / 2;
            var y = PageHeight - top - size * 0.8;
            _page.Append("BT ").Append(N(gray)).Append(" g /").Append(bold ? "F2 " : "F1 ").Append(N(size)).Append(" Tf ")
                 .Append(N(x)).Append(' ').Append(N(y)).Append(" Td (").Append(Escape(text)).Append(") Tj ET\n");
        }

        /// <summary>Text cut with "…" so that it fits in <paramref name="maxWidth"/>.</summary>
        public void TextFit(double x, double top, double size, string text, double maxWidth, bool bold = false, string align = "left")
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Width(text, size, bold) > maxWidth)
            {
                while (text.Length > 1 && Width(text + "…", size, bold) > maxWidth) text = text.Substring(0, text.Length - 1);
                text = text.TrimEnd() + "…";
            }
            Text(x, top, size, text, bold, align);
        }

        public void Line(double x1, double top1, double x2, double top2, double width = 0.6, double gray = 0.75)
        {
            _page.Append(N(gray)).Append(" G ").Append(N(width)).Append(" w ").Append(N(x1)).Append(' ').Append(N(PageHeight - top1))
                 .Append(" m ").Append(N(x2)).Append(' ').Append(N(PageHeight - top2)).Append(" l S\n");
        }

        /// <summary>A filled box; colour as r, g, b from 0 to 1.</summary>
        public void Box(double x, double top, double w, double h, double r, double g, double b)
        {
            _page.Append(N(r)).Append(' ').Append(N(g)).Append(' ').Append(N(b)).Append(" rg ")
                 .Append(N(x)).Append(' ').Append(N(PageHeight - top - h)).Append(' ').Append(N(w)).Append(' ').Append(N(h)).Append(" re f 0 g\n");
        }

        /// <summary>Draws a Code 128 barcode with its bars <paramref name="module"/> points wide.</summary>
        public void Barcode(double x, double top, string text, double module, double height)
        {
            var widths = Code128.Widths(text);
            var cx = x;
            for (var i = 0; i < widths.Count; i++)
            {
                if (i % 2 == 0) Box(cx, top, widths[i] * module, height, 0, 0, 0);
                cx += widths[i] * module;
            }
        }

        /// <summary>Adds a JPEG picture (bytes of a .jpg file with its pixel size) and draws it.</summary>
        public void Jpeg(byte[] jpeg, int pixelWidth, int pixelHeight, double x, double top, double w, double h)
        {
            var index = _images.IndexOf(jpeg);
            if (index < 0)
            {
                _images.Add(jpeg);
                _imageSizes.Add(new[] { pixelWidth, pixelHeight });
                index = _images.Count - 1;
            }
            _page.Append("q ").Append(N(w)).Append(" 0 0 ").Append(N(h)).Append(' ').Append(N(x)).Append(' ').Append(N(PageHeight - top - h))
                 .Append(" cm /Im").Append(index + 1).Append(" Do Q\n");
        }

        public byte[] ToBytes()
        {
            if (_pages.Count == 0) NewPage();
            var objects = new List<byte[]>();
            Func<string, int> add = s => { objects.Add(Encoding.GetEncoding(1252).GetBytes(s)); return objects.Count; };
            // 1 catalog, 2 pages, 3 font, 4 bold font, 5 info; then images, then page + content pairs.
            add("<< /Type /Catalog /Pages 2 0 R >>");
            add("PAGES");
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            add("<< /Producer (The Arqam Schools website) /Title (" + Escape(Title ?? "The Arqam Schools") + ") >>");
            var imageRefs = new StringBuilder();
            for (var i = 0; i < _images.Count; i++)
            {
                var head = "<< /Type /XObject /Subtype /Image /Width " + _imageSizes[i][0] + " /Height " + _imageSizes[i][1] +
                           " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + _images[i].Length + " >>\nstream\n";
                var bytes = new List<byte>(Encoding.ASCII.GetBytes(head));
                bytes.AddRange(_images[i]);
                bytes.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
                objects.Add(bytes.ToArray());
                imageRefs.Append("/Im").Append(i + 1).Append(' ').Append(objects.Count).Append(" 0 R ");
            }
            var kids = new StringBuilder();
            foreach (var page in _pages)
            {
                var content = Encoding.GetEncoding(1252).GetBytes(page.ToString());
                var cbytes = new List<byte>(Encoding.ASCII.GetBytes("<< /Length " + content.Length + " >>\nstream\n"));
                cbytes.AddRange(content);
                cbytes.AddRange(Encoding.ASCII.GetBytes("endstream"));
                objects.Add(cbytes.ToArray());
                var contentRef = objects.Count;
                add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + N(PageWidth) + " " + N(PageHeight) + "] /Contents " + contentRef +
                    " 0 R /Resources << /Font << /F1 3 0 R /F2 4 0 R >> /XObject << " + imageRefs + ">> >> >>");
                kids.Append(objects.Count).Append(" 0 R ");
            }
            objects[1] = Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [" + kids + "] /Count " + _pages.Count + " >>");

            using (var ms = new MemoryStream())
            {
                Action<string> write = s => { var b = Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); };
                write("%PDF-1.4\n");
                ms.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6); // marks the file as binary
                var offsets = new List<long>();
                for (var i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    write((i + 1) + " 0 obj\n");
                    ms.Write(objects[i], 0, objects[i].Length);
                    write("\nendobj\n");
                }
                var xref = ms.Position;
                write("xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
                foreach (var o in offsets) write(o.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                write("trailer\n<< /Size " + (objects.Count + 1) + " /Root 1 0 R /Info 5 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
                return ms.ToArray();
            }
        }

        // ---- Text helpers -------------------------------------------------------------------

        private static string Clean(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '−': sb.Append('-'); break;          // minus sign
                    case '–': case '—': case '…': case '·': case '’': case '‘': case '“': case '”':
                    case '×': sb.Append(ch); break;            // in WinAnsi
                    default: sb.Append(ch < 32 ? ' ' : (ch > 255 ? '?' : ch)); break;
                }
            }
            return sb.ToString();
        }

        private static string Escape(string text)
        {
            var bytes = Encoding.GetEncoding(1252).GetBytes(Clean(text));
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                if (b == '(' || b == ')' || b == '\\') sb.Append('\\').Append((char)b);
                else if (b < 32 || b > 126) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                else sb.Append((char)b);
            }
            return sb.ToString();
        }

        // Character widths (1/1000 em) of Helvetica and Helvetica-Bold for codes 32–126.
        private static readonly short[] Regular =
        {
            278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
            1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
            333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
        };
        private static readonly short[] Bold =
        {
            278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
            975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
            333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
        };

        public static double Width(string text, double size, bool bold)
        {
            double units = 0;
            foreach (var ch in Clean(text ?? ""))
            {
                var table = bold ? Bold : Regular;
                units += ch >= 32 && ch <= 126 ? table[ch - 32] : (ch == '…' || ch == '—' ? 1000 : 556);
            }
            return units * size / 1000.0;
        }
    }
}
