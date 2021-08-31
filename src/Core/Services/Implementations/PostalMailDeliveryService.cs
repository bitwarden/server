using System;
using System.Threading.Tasks;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class PostalMailDeliveryService : IMailDeliveryService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<PostalMailDeliveryService> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _baseTag;
        private readonly string _from;
        private readonly string _reply;

        public PostalMailDeliveryService(
            GlobalSettings globalSettings,
            ILogger<PostalMailDeliveryService> logger,
            IWebHostEnvironment hostingEnvironment,
            IHttpClientFactory clientFactory)
        {
            var postalDomain = CoreHelpers.PunyEncode(globalSettings.Mail.PostalDomain);
            var replyToEmail = CoreHelpers.PunyEncode(globalSettings.Mail.ReplyToEmail);

            _globalSettings = globalSettings;
            _logger = logger;
            _clientFactory = clientFactory;
            _baseTag = $"Env_{hostingEnvironment.EnvironmentName}-" +
                $"Server_{globalSettings.ProjectName?.Replace(' ', '_')}";
            _from = $"\"{globalSettings.SiteName}\" <no-reply@{postalDomain}>";
            _reply = $"\"{globalSettings.SiteName}\" <{replyToEmail}>";
        }

        public async Task SendEmailAsync(Models.Mail.MailMessage message)
        {
            var httpClient = _clientFactory.CreateClient("PostalMailDeliveryService");
            httpClient.DefaultRequestHeaders.Add("X-Server-API-Key", _globalSettings.Mail.PostalApiKey);

            var request = new PostalRequest
            {
                subject = message.Subject,
                from = _from,
                reply_to = _reply,
                html_body = message.HtmlContent,
                to = new List<string>(),
                tag = _baseTag
            };
            foreach (var address in message.ToEmails)
            {
                request.to.Add(CoreHelpers.PunyEncode(address));
            }

            if (message.BccEmails != null)
            {
                request.bcc = new List<string>();
                foreach (var address in message.BccEmails)
                {
                    request.bcc.Add(CoreHelpers.PunyEncode(address));
                }
            }

            if (!string.IsNullOrWhiteSpace(message.TextContent))
            {
                request.plain_body = message.TextContent;
            }

            if (!string.IsNullOrWhiteSpace(message.Category))
            {
                request.tag = string.Concat(request.tag, "-Cat_", message.Category);
            }

            var reqJson = JsonConvert.SerializeObject(request);
            var responseMessage = await httpClient.PostAsync(
                $"https://{_globalSettings.Mail.PostalDomain}/api/v1/send/message",
                new StringContent(reqJson, Encoding.UTF8, "application/json"));

            if (responseMessage.IsSuccessStatusCode)
            {
                var json = await responseMessage.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<PostalResponse>(json);
                if (response.status != "success")
                {
                    _logger.LogError("Postal send status was not successful: {0}, {1}",
                        response.status, response.message);
                }
            }
            else
            {
                _logger.LogError("Postal send failed: {0}", responseMessage.StatusCode);
            }
        }

        public class PostalRequest
        {
            public List<string> to { get; set; }
            public List<string> cc { get; set; }
            public List<string> bcc { get; set; }
            public string tag { get; set; }
            public string from { get; set; }
            public string reply_to { get; set; }
            public string plain_body { get; set; }
            public string html_body { get; set; }
            public string subject { get; set; }
        }

        public class PostalResponse
        {
            public string status { get; set; }
            public string message { get; set; }
        }
    }
}
