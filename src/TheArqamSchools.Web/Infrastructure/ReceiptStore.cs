using System;
using System.Configuration;
using System.IO;
using System.Web.Hosting;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Payment receipts (photo or PDF) for book payments. They are kept under App_Data (Storage:PrivateRoot),
    /// which the web server never serves, with random names; only the admin panel streams them back.
    /// The file type is checked by its first bytes, not by its name.
    /// </summary>
    public static class ReceiptStore
    {
        public const int MaxBytes = 10 * 1024 * 1024;

        public static string Save(Stream input, int length, out string mime)
        {
            mime = null;
            if (length <= 0) return null;
            if (length > MaxBytes) throw new ImageRejectedException("The receipt is larger than 10 MB. Please choose a smaller file.");
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                bytes = ms.ToArray();
            }
            if (bytes.Length > MaxBytes) throw new ImageRejectedException("The receipt is larger than 10 MB. Please choose a smaller file.");
            string ext;
            if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) { mime = "image/jpeg"; ext = ".jpg"; }
            else if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) { mime = "image/png"; ext = ".png"; }
            else if (bytes.Length > 5 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-') { mime = "application/pdf"; ext = ".pdf"; }
            else throw new ImageRejectedException("The receipt must be a photo (JPG or PNG) or a PDF.");
            var now = DateTime.UtcNow.AddHours(5);
            var relative = "book-receipts/" + now.ToString("yyyy") + "/" + now.ToString("MM") + "/" + Guid.NewGuid().ToString("N") + ext;
            var full = PhysicalPath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
            return relative;
        }

        public static string PhysicalPath(string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Contains("..") || relative.Contains("\\") || relative.StartsWith("/"))
                throw new InvalidOperationException("Invalid receipt path.");
            var root = ConfigurationManager.AppSettings["Storage:PrivateRoot"];
            if (string.IsNullOrWhiteSpace(root)) root = "~/App_Data/private";
            var rootPath = root.StartsWith("~") ? HostingEnvironment.MapPath(root) : root;
            return Path.Combine(rootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public static void Delete(string relative)
        {
            try { var f = PhysicalPath(relative); if (File.Exists(f)) File.Delete(f); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
