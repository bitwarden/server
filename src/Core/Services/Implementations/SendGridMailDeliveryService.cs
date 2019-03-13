using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using Bit.Core.Models.Mail;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace Bit.Core.Services
{
    public class SendGridMailDeliveryService : IMailDeliveryService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly SendGridClient _client;

        public SendGridMailDeliveryService(GlobalSettings globalSettings)
        {
            if(globalSettings.Mail?.SendGridApiKey == null)
            {
                throw new ArgumentNullException(nameof(globalSettings.Mail.SendGridApiKey));
            }

            _globalSettings = globalSettings;
            _client = new SendGridClient(_globalSettings.Mail.SendGridApiKey);
        }

        public async Task SendEmailAsync(MailMessage message)
        {
            var sendGridMessage = new SendGridMessage
            {
                Subject = message.Subject,
                From = new EmailAddress(_globalSettings.Mail.ReplyToEmail, _globalSettings.SiteName),
                HtmlContent = message.HtmlContent,
                PlainTextContent = message.TextContent
            };

            sendGridMessage.SetClickTracking(true, false);
            sendGridMessage.SetOpenTracking(true, null);
            sendGridMessage.AddTos(message.ToEmails.Select(e => new EmailAddress(e)).ToList());
            if(message.BccEmails?.Any() ?? false)
            {
                sendGridMessage.AddBccs(message.BccEmails.Select(e => new EmailAddress(e)).ToList());
            }

            if(message.MetaData?.ContainsKey("SendGridTemplateId") ?? false)
            {
                sendGridMessage.HtmlContent = " ";
                sendGridMessage.PlainTextContent = " ";
                sendGridMessage.TemplateId = message.MetaData["SendGridTemplateId"].ToString();
            }

            if(message.MetaData?.ContainsKey("SendGridSubstitutions") ?? false)
            {
                var subs = message.MetaData["SendGridSubstitutions"] as Dictionary<string, string>;
                sendGridMessage.AddSubstitutions(subs);
            }

            var cats = new List<string> { "Bitwarden Server" };
            if(!string.IsNullOrWhiteSpace(message.Category))
            {
                cats.Add(message.Category);
            }
            sendGridMessage.AddCategories(cats);

            if(message.MetaData?.ContainsKey("SendGridBypassListManagement") ?? false)
            {
                var bypass = message.MetaData["SendGridBypassListManagement"] as bool?;
                sendGridMessage.SetBypassListManagement(bypass.GetValueOrDefault(false));
            }

            try
            {
                await SendAsync(sendGridMessage, false);
            }
            catch(HttpRequestException)
            {
                await SendAsync(sendGridMessage, true);
            }
            catch(WebException)
            {
                await SendAsync(sendGridMessage, true);
            }
        }

        private async Task SendAsync(SendGridMessage sendGridMessage, bool retry)
        {
            if(retry)
            {
                // wait and try again
                await Task.Delay(2000);
            }

            await _client.SendEmailAsync(sendGridMessage);
        }
    }
}
