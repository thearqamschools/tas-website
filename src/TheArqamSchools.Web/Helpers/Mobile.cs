using System.Linq;

namespace TheArqamSchools.Web.Helpers
{
    /// <summary>
    /// Pakistani mobile numbers for the books system. Stored in one format, 11 digits starting
    /// with 03 (03001234567), and shown as 0300-1234567. "+92 300 1234567" and "0300-1234567" are accepted.
    /// </summary>
    public static class Mobile
    {
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("0092")) digits = "0" + digits.Substring(4);
            else if (digits.StartsWith("92") && digits.Length == 12) digits = "0" + digits.Substring(2);
            else if (digits.StartsWith("3") && digits.Length == 10) digits = "0" + digits;
            return digits.Length == 11 && digits.StartsWith("03") ? digits : null;
        }

        public static string Format(string stored)
        {
            return string.IsNullOrEmpty(stored) || stored.Length != 11 ? stored : stored.Substring(0, 4) + "-" + stored.Substring(4);
        }
    }
}
