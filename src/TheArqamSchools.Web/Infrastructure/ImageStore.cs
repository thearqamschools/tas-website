using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Hosting;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>One photo saved by <see cref="ImageStore"/>: a large copy and a small copy for cards and thumbnails.</summary>
    public class StoredImage
    {
        /// <summary>Path inside the public-media bucket, for example news/2026/11/3f2a….jpg.</summary>
        public string StoragePath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; }
        public string OriginalFileName { get; set; }
    }

    /// <summary>Thrown when an uploaded file is not a photo we accept. The message is shown to the admin user.</summary>
    public class ImageRejectedException : Exception
    {
        public ImageRejectedException(string message) : base(message) { }
    }

    /// <summary>
    /// Saves uploaded photos for news posts and events. Every photo is decoded, turned the right
    /// way up (EXIF orientation), made smaller and saved again as JPEG. Saving again removes the
    /// camera data in the file (including GPS location) and anything that is not a picture.
    /// Files get random names, so an upload can never replace another file. The small copy sits
    /// next to the large one with "-sm" before ".jpg" (see <see cref="SmallPath"/>).
    /// </summary>
    public static class ImageStore
    {
        public const int MaxUploadBytes = 15 * 1024 * 1024;
        public const int LargeSide = 1920;
        public const int SmallSide = 640;
        /// <summary>Pictures larger than this (in pixels) are refused before they are decoded, so a tiny file cannot fill the memory.</summary>
        private const long MaxPixels = 40L * 1000 * 1000;
        /// <summary>Only one photo is decoded at a time, so two uploads together cannot run the shared host out of memory.</summary>
        private static readonly System.Threading.SemaphoreSlim Gate = new System.Threading.SemaphoreSlim(1, 1);
        private const int OrientationTag = 0x0112;

        public static string SmallPath(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath)) return storagePath;
            return storagePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? storagePath.Substring(0, storagePath.Length - 4) + "-sm.jpg"
                : storagePath;
        }

        /// <summary>
        /// Checks, resizes and saves one photo under public-media/{folder}/yyyy/MM/. With
        /// <paramref name="keepTransparency"/>, a PNG stays a PNG (for seals, badges and logos with a
        /// transparent background) and no small copy is made.
        /// </summary>
        public static StoredImage Save(Stream input, long length, string originalName, string folder, bool keepTransparency = false)
        {
            if (input == null || length <= 0) throw new ImageRejectedException("The file is empty.");
            if (length > MaxUploadBytes) throw new ImageRejectedException("The photo is larger than 15 MB. Please choose a smaller photo.");
            var bytes = ReadAll(input, MaxUploadBytes);
            if (bytes == null) throw new ImageRejectedException("The photo is larger than 15 MB. Please choose a smaller photo.");

            int w, h;
            if (!HeaderSize(bytes, out w, out h))
                throw new ImageRejectedException("Only JPG and PNG photos can be uploaded. Please save the photo as JPG and try again.");
            if (w <= 0 || h <= 0 || (long)w * h > MaxPixels)
                throw new ImageRejectedException("The photo is too large in pixels. Please choose a photo under 40 megapixels.");

            var png = keepTransparency && bytes[0] == 0x89;
            var now = DateTime.UtcNow.AddHours(5);
            var relative = folder + "/" + now.ToString("yyyy") + "/" + now.ToString("MM") + "/" + Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg");
            var largeFile = PhysicalPath(relative);
            var smallFile = PhysicalPath(SmallPath(relative));
            Directory.CreateDirectory(Path.GetDirectoryName(largeFile));

            var result = new StoredImage { StoragePath = relative, OriginalFileName = CleanName(originalName) };
            if (!Gate.Wait(TimeSpan.FromSeconds(60))) throw new ImageRejectedException("The server is busy with other photos. Please try again in a minute.");
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var source = LoadImage(ms))
                {
                    ApplyOrientation(source, JpegOrientation(bytes));
                    using (var large = Resize(source, LargeSide, png))
                    {
                        if (png) large.Save(largeFile, ImageFormat.Png);
                        else WriteJpeg(large, largeFile, 82L);
                        result.Width = large.Width;
                        result.Height = large.Height;
                    }
                    if (!png) using (var small = Resize(source, SmallSide, false)) WriteJpeg(small, smallFile, 78L);
                }
                var info = new FileInfo(largeFile);
                result.SizeBytes = info.Length;
                using (var sha = SHA256.Create())
                using (var f = File.OpenRead(largeFile))
                    result.Sha256 = string.Concat(sha.ComputeHash(f).Select(b => b.ToString("x2")));
                return result;
            }
            catch (OutOfMemoryException)
            {
                Delete(relative);
                throw new ImageRejectedException("This photo is too large for the server to process. Please choose a smaller photo.");
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                Delete(relative);
                throw new ImageRejectedException("This photo could not be processed. Please save it as JPG and try again.");
            }
            catch
            {
                Delete(relative);
                throw;
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        /// Moves the files of a removed photo out of the public folder into App_Data (never served),
        /// so old links stop working. The files are kept, not deleted. Returns false when they could not be moved.
        /// </summary>
        public static bool Archive(string storagePath)
        {
            var ok = true;
            foreach (var p in new[] { storagePath, SmallPath(storagePath) })
            {
                try
                {
                    var from = PhysicalPath(p);
                    if (!File.Exists(from)) continue;
                    var to = Path.Combine(HostingEnvironment.MapPath("~/App_Data") ?? "App_Data", "removed-media", "public-media", p.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(from, to);
                }
                catch (IOException) { ok = false; }
                catch (UnauthorizedAccessException) { ok = false; }
            }
            return ok;
        }

        /// <summary>Removes the files of a photo that was saved but could not be recorded in the database.</summary>
        public static void Delete(string storagePath)
        {
            foreach (var p in new[] { storagePath, SmallPath(storagePath) })
            {
                try { var f = PhysicalPath(p); if (File.Exists(f)) File.Delete(f); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string PhysicalPath(string relative)
        {
            if (relative.Contains("..") || relative.Contains("\\") || relative.StartsWith("/"))
                throw new InvalidOperationException("Invalid storage path.");
            var root = ConfigurationManager.AppSettings["Storage:Root"];
            if (string.IsNullOrWhiteSpace(root)) root = "~/media";
            var rootPath = root.StartsWith("~") ? HostingEnvironment.MapPath(root) : root;
            return Path.Combine(rootPath, "public-media", relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static byte[] ReadAll(Stream input, int max)
        {
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[81920];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (ms.Length + read > max) return null;
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private static Image LoadImage(Stream ms)
        {
            try { return Image.FromStream(ms, false, true); }
            catch (ArgumentException) { throw new ImageRejectedException("This file could not be read as a photo. Please save it as JPG and try again."); }
            catch (OutOfMemoryException) { throw new ImageRejectedException("This file could not be read as a photo. Please save it as JPG and try again."); }
        }

        /// <summary>
        /// Reads the size from the file header (PNG IHDR or the JPEG SOF marker) without decoding.
        /// Returns false for anything that is not a JPEG or PNG.
        /// </summary>
        private static bool HeaderSize(byte[] b, out int w, out int h)
        {
            w = h = 0;
            if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            {
                w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return true;
            }
            if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            {
                var i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var marker = b[i + 1];
                    if (marker == 0xFF) { i++; continue; }
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                    var len = (b[i + 2] << 8) | b[i + 3];
                    if (len < 2) return false;
                    // SOF0..SOF15, except DHT (C4), JPG (C8) and DAC (CC).
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        h = (b[i + 5] << 8) | b[i + 6];
                        w = (b[i + 7] << 8) | b[i + 8];
                        return true;
                    }
                    i += 2 + len;
                }
            }
            return false;
        }

        /// <summary>
        /// Reads the EXIF orientation (1-8) straight from the JPEG's APP1 segment, so it does not
        /// depend on what the graphics library exposes. Returns 0 when there is none.
        /// </summary>
        private static int JpegOrientation(byte[] b)
        {
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return 0;
            var i = 2;
            while (i + 4 < b.Length && b[i] == 0xFF)
            {
                var marker = b[i + 1];
                var len = (b[i + 2] << 8) | b[i + 3];
                if (marker == 0xDA || len < 2) return 0;
                var start = i + 4;
                if (marker == 0xE1 && start + 14 < b.Length && b[start] == 'E' && b[start + 1] == 'x' && b[start + 2] == 'i' && b[start + 3] == 'f' && b[start + 4] == 0 && b[start + 5] == 0)
                {
                    var t = start + 6;
                    var little = b[t] == 'I' && b[t + 1] == 'I';
                    if (!little && !(b[t] == 'M' && b[t + 1] == 'M')) return 0;
                    var end = Math.Min(b.Length, i + 2 + len);
                    Func<int, int> u16 = o => o + 1 < end ? (little ? b[o] | (b[o + 1] << 8) : (b[o] << 8) | b[o + 1]) : -1;
                    Func<int, long> u32 = o => o + 3 < end ? (little ? b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | ((long)b[o + 3] << 24) : ((long)b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]) : -1;
                    var ifdOffset = u32(t + 4);
                    if (ifdOffset < 8 || t + ifdOffset >= end) return 0;
                    var ifd = t + (int)ifdOffset;
                    var count = u16(ifd);
                    for (var k = 0; k < count && k < 200; k++)
                    {
                        var e = ifd + 2 + k * 12;
                        if (e + 12 > end) return 0;
                        if (u16(e) == 0x0112) { var v = u16(e + 8); return v >= 1 && v <= 8 ? v : 0; }
                    }
                    return 0;
                }
                i += 2 + len;
            }
            return 0;
        }

        private static void ApplyOrientation(Image img, int fromFile)
        {
            var value = fromFile;
            if (value == 0 && img.PropertyIdList.Contains(OrientationTag))
            {
                var prop = img.GetPropertyItem(OrientationTag);
                if (prop.Value != null && prop.Value.Length >= 2) value = prop.Value[0] | (prop.Value[1] << 8);
            }
            switch (value)
            {
                case 2: img.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: img.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: img.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: img.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: img.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: img.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: img.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }

        private static Bitmap Resize(Image source, int maxSide, bool alpha)
        {
            var scale = Math.Min(1.0, (double)maxSide / Math.Max(source.Width, source.Height));
            var w = Math.Max(1, (int)Math.Round(source.Width * scale));
            var h = Math.Max(1, (int)Math.Round(source.Height * scale));
            var bmp = new Bitmap(w, h, alpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                using (var attrs = new ImageAttributes())
                {
                    // White behind transparent PNGs, since JPEG has no transparency.
                    g.Clear(alpha ? Color.Transparent : Color.White);
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    attrs.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(source, new Rectangle(0, 0, w, h), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
                }
                return bmp;
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
        }

        private static void WriteJpeg(Image img, string file, long quality)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var p = new EncoderParameters(1))
            {
                p.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                img.Save(file, codec, p);
            }
        }

        private static string CleanName(string name)
        {
            // Browsers may send a full path ("C:\\fakepath\\photo.jpg"); keep only the last part.
            name = name ?? "";
            var cut = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (cut >= 0) name = name.Substring(cut + 1);
            name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (name.Length == 0) name = "photo.jpg";
            return name.Length > 200 ? name.Substring(name.Length - 200) : name;
        }
    }
}
