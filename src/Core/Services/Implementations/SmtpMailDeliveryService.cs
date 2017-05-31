using System;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace Bit.Core.Services
{
    public class SmtpMailDeliveryService : IMailDeliveryService
    {
        private readonly GlobalSettings _globalSettings;

        public SmtpMailDeliveryService(GlobalSettings globalSettings)
        {
            if(globalSettings.Mail?.Smtp?.Host == null)
            {
                throw new ArgumentNullException(nameof(globalSettings.Mail.Smtp.Host));
            }

            _globalSettings = globalSettings;
        }

        public Task SendEmailAsync(Models.Mail.MailMessage message)
        {
            using(var client = new SmtpClient(_globalSettings.Mail.Smtp.Host, _globalSettings.Mail.Smtp.Port))
            {
                client.UseDefaultCredentials = false;
                client.EnableSsl = _globalSettings.Mail.Smtp.Ssl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Credentials = new NetworkCredential(_globalSettings.Mail.Smtp.Username,
                    _globalSettings.Mail.Smtp.Password);

                var smtpMessage = new MailMessage();
                smtpMessage.From = new MailAddress(_globalSettings.Mail.ReplyToEmail, _globalSettings.SiteName);
                smtpMessage.Subject = message.Subject;
                smtpMessage.SubjectEncoding = Encoding.UTF8;
                smtpMessage.BodyEncoding = Encoding.UTF8;
                smtpMessage.BodyTransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;
                foreach(var address in message.ToEmails)
                {
                    smtpMessage.To.Add(new MailAddress(address));
                }

                if(string.IsNullOrWhiteSpace(message.TextContent))
                {
                    smtpMessage.IsBodyHtml = true;
                    smtpMessage.Body = message.HtmlContent;
                }
                else
                {
                    smtpMessage.Body = message.TextContent;
                    var htmlView = AlternateView.CreateAlternateViewFromString(message.HtmlContent);
                    htmlView.ContentType = new System.Net.Mime.ContentType("text/html");
                    smtpMessage.AlternateViews.Add(htmlView);
                }

                client.SendCompleted += (s, e) =>
                {
                    smtpMessage.Dispose();
                };

                client.SendAsync(smtpMessage, null);
                return Task.FromResult(0);
            }
        }
    }
}
