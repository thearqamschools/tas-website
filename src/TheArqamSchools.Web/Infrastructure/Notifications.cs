using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web.Hosting;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// Emails the school when a visitor sends an admission inquiry or a message, so
    /// nobody has to keep checking the admin inbox. Off until the server has mail
    /// settings (appSettings Mail:Host, Mail:Port, Mail:User, Mail:Password, Mail:From,
    /// Mail:NotifyTo, Mail:EnableSsl). The email carries only the name, campus and kind
    /// of request plus a link to the admin inbox; phone numbers and messages stay in
    /// the admin panel behind sign-in. Sending happens after the visitor's page has
    /// been answered, and a mail failure never affects the visitor (it is only logged).
    /// </summary>
    public static class Notifications
    {
        public static bool Enabled
        {
            get { return !string.IsNullOrWhiteSpace(Setting("Mail:Host")) && !string.IsNullOrWhiteSpace(Setting("Mail:NotifyTo")) && !string.IsNullOrWhiteSpace(Setting("Mail:From")); }
        }

        public static void AdmissionInquiry(Guid id, string name, string campus, string className)
        {
            var body = new StringBuilder()
                .AppendLine("A new admission inquiry was sent from the website.")
                .AppendLine()
                .AppendLine("Name: " + OneLine(name))
                .AppendLine("Campus: " + (string.IsNullOrEmpty(campus) ? "not chosen" : OneLine(campus)))
                .AppendLine("Class: " + (string.IsNullOrEmpty(className) ? "not chosen" : OneLine(className)))
                .AppendLine()
                .AppendLine("Open it in the admin panel to see the phone number and message:")
                .AppendLine(Helpers.Site.Absolute("/admin/inquiries/" + id.ToString("D")));
            Send("New admission inquiry" + (string.IsNullOrEmpty(campus) ? "" : " – " + OneLine(campus)), body.ToString());
        }

        public static void ContactMessage(Guid id, string type, string name, string campus)
        {
            var body = new StringBuilder()
                .AppendLine("A new message was sent from the website.")
                .AppendLine()
                .AppendLine("Type: " + OneLine(type))
                .AppendLine("Name: " + OneLine(name))
                .AppendLine("Campus: " + (string.IsNullOrEmpty(campus) ? "not chosen" : OneLine(campus)))
                .AppendLine()
                .AppendLine("Open it in the admin panel to read it and reply:")
                .AppendLine(Helpers.Site.Absolute("/admin/messages/" + id.ToString("D")));
            Send("New website message (" + OneLine(type) + ")", body.ToString());
        }

        public static void FranchiseApplication(Guid id, string reference, string name, string proposedCity)
        {
            var body = new StringBuilder()
                .AppendLine("A new franchise application was sent from the website.")
                .AppendLine()
                .AppendLine("Reference: " + OneLine(reference))
                .AppendLine("Name: " + OneLine(name))
                .AppendLine("Proposed city: " + OneLine(proposedCity))
                .AppendLine()
                .AppendLine("Open it in the admin panel to see the contact details and full application:")
                .AppendLine(Helpers.Site.Absolute("/admin/franchise/" + id.ToString("D")));
            Send("New franchise application " + OneLine(reference), body.ToString());
        }

        private static void Send(string subject, string body)
        {
            if (!Enabled) return;
            var host = Setting("Mail:Host");
            int port;
            if (!int.TryParse(Setting("Mail:Port"), out port)) port = 587;
            var user = Setting("Mail:User");
            var password = Setting("Mail:Password");
            var from = Setting("Mail:From");
            var to = Setting("Mail:NotifyTo");
            var ssl = !string.Equals(Setting("Mail:EnableSsl"), "false", StringComparison.OrdinalIgnoreCase);

            HostingEnvironment.QueueBackgroundWorkItem(ct =>
            {
                try
                {
                    using (var message = new MailMessage())
                    using (var client = new SmtpClient(host, port))
                    {
                        message.From = new MailAddress(from, "The Arqam Schools website");
                        foreach (var address in to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).Where(a => a.Length > 0))
                            message.To.Add(address);
                        message.Subject = subject;
                        message.Body = body;
                        message.BodyEncoding = Encoding.UTF8;
                        message.SubjectEncoding = Encoding.UTF8;
                        message.IsBodyHtml = false;
                        client.EnableSsl = ssl;
                        client.Timeout = 20000;
                        if (!string.IsNullOrEmpty(user))
                            client.Credentials = new NetworkCredential(user, password);
                        client.Send(message);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Notification email failed: {0}", ex.GetType().FullName + ": " + ex.Message);
                }
            });
        }

        private static string Setting(string key)
        {
            var v = ConfigurationManager.AppSettings[key];
            return v == null ? null : v.Trim();
        }

        /// <summary>Visitor text in a subject or line: no line breaks (prevents header injection), at most 120 characters.</summary>
        private static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return s.Length > 120 ? s.Substring(0, 120) + "…" : s;
        }
    }
}
