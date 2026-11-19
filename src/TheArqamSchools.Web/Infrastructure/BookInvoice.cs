using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using TheArqamSchools.Web.Helpers;
using TheArqamSchools.Web.Models;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// The book invoice as a PDF: logo and name, the order ID with its barcode, date, customer, pick up or
    /// delivery, the books, and total, discount, paid and balance. Contact details are exactly those set by
    /// the owner for book invoices (Faisalabad office).
    /// </summary>
    public static class BookInvoice
    {
        public static readonly string[] Contact =
        {
            "The Arqam Schools, Sanora Colony, Samanabad, Faisalabad, Pakistan",
            "Phone: 041-2660265, 041-2406479",
            "WhatsApp: 0339-2406479",
            "www.thearqam.com"
        };

        private static byte[] _logo;
        private static int _logoW, _logoH;
        private static readonly object LogoLock = new object();

        public static byte[] Pdf(BookOrder o)
        {
            var pk = TimeSpan.FromHours(5);
            var pdf = new SimplePdf { Title = "Invoice " + o.OrderLabel };
            pdf.NewPage();
            const double left = 40, right = 555;
            var top = 40.0;

            LoadLogo();
            if (_logo != null) pdf.Jpeg(_logo, _logoW, _logoH, left, top, 56, 56);
            pdf.Text(left + 68, top + 2, 17, "THE ARQAM SCHOOLS", true, "left", 0.15);
            for (var i = 0; i < Contact.Length; i++) pdf.Text(left + 68, top + 24 + i * 11, 8.5, Contact[i], false, "left", 0.3);

            pdf.Text(right, top, 20, "INVOICE", true, "right", 0.15);
            pdf.Text(right, top + 26, 10, "Order ID: " + o.OrderLabel, true, "right");
            pdf.Text(right, top + 40, 9.5, "Date: " + o.CreatedAt.ToOffset(pk).ToString("d MMMM yyyy", CultureInfo.InvariantCulture), false, "right");
            // Barcode of the order ID, right-aligned under the date.
            var code = o.OrderLabel;
            var modules = Code128.Widths(code).Sum();
            const double module = 1.1;
            pdf.Barcode(right - modules * module, top + 56, code, module, 34);
            pdf.Text(right - modules * module / 2, top + 92, 8, code, false, "center");

            top = 150;
            pdf.Line(left, top, right, top, 1, 0.8);
            top += 10;
            pdf.Text(left, top, 8, "BILL TO", true, "left", 0.45);
            pdf.Text(330, top, 8, "DELIVERY", true, "left", 0.45);
            top += 13;
            pdf.Text(left, top, 11, o.CustomerName, true);
            pdf.Text(330, top, 11, BookFormat.FulfilmentLabel(o.Fulfilment), true);
            pdf.Text(left, top + 15, 9.5, "Mobile: " + Mobile.Format(o.CustomerMobile));
            if (!string.IsNullOrEmpty(o.CampusName)) pdf.TextFit(left, top + 28, 9.5, o.CampusName, 270);
            if (o.Fulfilment == "delivery" && !string.IsNullOrEmpty(o.DeliveryAddress))
            {
                var lines = Wrap(o.DeliveryAddress, 9.5, 225);
                for (var i = 0; i < lines.Length && i < 4; i++) pdf.Text(330, top + 15 + i * 12, 9.5, lines[i]);
            }
            else pdf.Text(330, top + 15, 9.5, "Collect from the office");

            top += 70;
            // Table header
            double cClass = left, cBook = left + 85, cQty = 400, cPrice = 470, cTotal = right;
            Action header = () =>
            {
                pdf.Box(left, top, right - left, 18, 0.93, 0.94, 0.98);
                pdf.Text(cClass + 4, top + 5, 8.5, "CLASS", true);
                pdf.Text(cBook, top + 5, 8.5, "BOOK", true);
                pdf.Text(cQty, top + 5, 8.5, "QTY", true, "right");
                pdf.Text(cPrice, top + 5, 8.5, "PRICE", true, "right");
                pdf.Text(cTotal - 4, top + 5, 8.5, "TOTAL", true, "right");
                top += 22;
            };
            header();
            foreach (var l in o.Lines)
            {
                if (top > 720)
                {
                    pdf.Text(right, 810, 8, "Continued on the next page", false, "right", 0.45);
                    pdf.NewPage();
                    top = 40;
                    pdf.Text(left, top, 11, "Invoice " + o.OrderLabel + " (continued)", true);
                    top += 22;
                    header();
                }
                pdf.TextFit(cClass + 4, top, 9.5, l.ClassName, 78);
                pdf.TextFit(cBook, top, 9.5, l.ItemName, cQty - cBook - 34);
                pdf.Text(cQty, top, 9.5, l.Quantity.ToString(CultureInfo.InvariantCulture) + (l.ReturnedQty > 0 ? " (" + l.ReturnedQty + " returned)" : ""), false, "right");
                pdf.Text(cPrice, top, 9.5, Amount(l.UnitPrice), false, "right");
                pdf.Text(cTotal - 4, top, 9.5, Amount(l.LineTotal), false, "right");
                top += 15;
                pdf.Line(left, top - 3, right, top - 3, 0.4, 0.88);
            }

            if (top > 680) { pdf.NewPage(); top = 40; }
            top += 8;
            Action<string, decimal, bool> row = (label, value, strong) =>
            {
                pdf.Text(cPrice, top, strong ? 11 : 9.5, label, strong, "right");
                pdf.Text(cTotal - 4, top, strong ? 11 : 9.5, Amount(value), strong, "right");
                top += strong ? 18 : 14;
            };
            row("Total", o.Subtotal, false);
            if (o.ReturnedTotal > 0) row("Returned", -o.ReturnedTotal, false);
            if (o.DiscountTotal > 0) row("Discount", -o.DiscountTotal, false);
            if (o.CreditApplied > 0) row("Credit", -o.CreditApplied, false);
            row("Paid", o.PaidTotal, false);
            if (o.RefundedTotal > 0) row("Refunded", o.RefundedTotal, false);
            pdf.Line(cQty - 40, top - 2, right, top - 2, 0.8, 0.6);
            top += 4;
            row("Balance", o.Balance, true);

            pdf.Text(left, 800, 8.5, "Thank you for your order. Amounts are in Pakistani rupees.", false, "left", 0.45);
            return pdf.ToBytes();
        }

        public static string Amount(decimal v)
        {
            return (v < 0 ? "- " : "") + "Rs. " + Math.Abs(v).ToString("#,##0.##", CultureInfo.InvariantCulture);
        }

        private static string[] Wrap(string text, double size, double width)
        {
            var words = (text ?? "").Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new System.Collections.Generic.List<string>();
            var cur = "";
            foreach (var w in words)
            {
                var next = cur.Length == 0 ? w : cur + " " + w;
                if (SimplePdf.Width(next, size, false) > width && cur.Length > 0) { lines.Add(cur); cur = w; }
                else cur = next;
            }
            if (cur.Length > 0) lines.Add(cur);
            return lines.ToArray();
        }

        /// <summary>The official logo as a JPEG on white (PDF keeps it simple); made once and kept in memory.</summary>
        private static void LoadLogo()
        {
            if (_logo != null) return;
            lock (LogoLock)
            {
                if (_logo != null) return;
                try
                {
                    var path = HostingEnvironment.MapPath("~/Content/brand/the-arqam-schools-logo-192.png");
                    if (path == null || !File.Exists(path)) return;
                    using (var src = Image.FromFile(path))
                    using (var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.White); g.DrawImage(src, 0, 0, src.Width, src.Height); }
                        using (var ms = new MemoryStream())
                        {
                            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            using (var p = new EncoderParameters(1))
                            {
                                p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                                bmp.Save(ms, codec, p);
                            }
                            _logoW = bmp.Width; _logoH = bmp.Height;
                            _logo = ms.ToArray();
                        }
                    }
                }
                catch (Exception) { _logo = null; }
            }
        }
    }
}
