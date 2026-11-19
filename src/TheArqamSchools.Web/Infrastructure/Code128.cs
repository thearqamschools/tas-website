using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Code 128 (set B) barcodes for book invoices: any normal USB barcode scanner reads them and types
    /// the order ID (for example BK-1001) into the payment search box. Output is a list of bar widths
    /// (in modules), drawn as SVG for the web page and as rectangles in the PDF.
    /// </summary>
    public static class Code128
    {
        // Widths of bar, space, bar, space, bar, space for values 0–106 (106 = stop, which has a 7th bar).
        private static readonly string[] Patterns =
        {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        /// <summary>Bar and space widths, starting with a bar, for printable ASCII text (32–126).</summary>
        public static IList<int> Widths(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Empty barcode text.");
            var codes = new List<int> { 104 }; // Start B
            foreach (var ch in text)
            {
                if (ch < 32 || ch > 126) throw new ArgumentException("Code 128 B supports printable ASCII only.");
                codes.Add(ch - 32);
            }
            var sum = 104;
            for (var i = 1; i < codes.Count; i++) sum += codes[i] * i;
            codes.Add(sum % 103);
            codes.Add(106); // Stop
            var widths = new List<int>();
            foreach (var c in codes)
                foreach (var d in Patterns[c]) widths.Add(d - '0');
            return widths;
        }

        /// <summary>An SVG barcode (black bars on white) with quiet zones, scaled by CSS.</summary>
        public static string Svg(string text, int height = 60)
        {
            var widths = Widths(text);
            const int quiet = 10;
            var total = quiet * 2;
            foreach (var w in widths) total += w;
            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"barcode\" viewBox=\"0 0 ").Append(total).Append(' ').Append(height)
              .Append("\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"Barcode ").Append(System.Web.HttpUtility.HtmlAttributeEncode(text)).Append("\">")
              .Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>");
            var x = quiet;
            for (var i = 0; i < widths.Count; i++)
            {
                if (i % 2 == 0)
                    sb.Append("<rect x=\"").Append(x.ToString(CultureInfo.InvariantCulture)).Append("\" y=\"0\" width=\"")
                      .Append(widths[i].ToString(CultureInfo.InvariantCulture)).Append("\" height=\"").Append(height).Append("\" fill=\"#000\"/>");
                x += widths[i];
            }
            sb.Append("</svg>");
            return sb.ToString();
        }
    }
}
