using System.Linq;

namespace TheArqamSchools.Web.Helpers
{
    public static class Contact
    {
        /// <summary>tel: link: digits and a leading + only.</summary>
        public static string TelUrl(string phone)
        {
            return "tel:" + new string((phone ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        }

        /// <summary>
        /// wa.me link with an international number: 0300 1234567 -> 923001234567,
        /// 0092... -> 92..., +92... -> 92...
        /// </summary>
        public static string WhatsAppUrl(string phone)
        {
            var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
            if (digits.StartsWith("00")) digits = digits.Substring(2);
            else if (digits.StartsWith("0")) digits = "92" + digits.Substring(1);
            return "https://wa.me/" + digits;
        }
    }
}
