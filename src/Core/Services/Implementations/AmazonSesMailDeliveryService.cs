using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using System.Linq;
using Amazon.SimpleEmail;
using Amazon;
using Amazon.SimpleEmail.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AmazonSesMailDeliveryService : IMailDeliveryService, IDisposable
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<AmazonSesMailDeliveryService> _logger;
        private readonly IAmazonSimpleEmailService _client;
        private readonly string _source;
        private readonly string _senderTag;
        private readonly string _configSetName;
        private const int MAX_BATCH_MESSAGE_RECIPIENTS = 50;

        public AmazonSesMailDeliveryService(
            GlobalSettings globalSettings,
            IWebHostEnvironment hostingEnvironment,
            ILogger<AmazonSesMailDeliveryService> logger)
        : this(globalSettings, hostingEnvironment, logger,
              new AmazonSimpleEmailServiceClient(
                globalSettings.Amazon.AccessKeyId,
                globalSettings.Amazon.AccessKeySecret,
                RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region))
              )
        {
        }

        public AmazonSesMailDeliveryService(
            GlobalSettings globalSettings,
            IWebHostEnvironment hostingEnvironment,
            ILogger<AmazonSesMailDeliveryService> logger,
            IAmazonSimpleEmailService amazonSimpleEmailService)
        {
            if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeyId))
            {
                throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeyId));
            }
            if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.AccessKeySecret))
            {
                throw new ArgumentNullException(nameof(globalSettings.Amazon.AccessKeySecret));
            }
            if (string.IsNullOrWhiteSpace(globalSettings.Amazon?.Region))
            {
                throw new ArgumentNullException(nameof(globalSettings.Amazon.Region));
            }

            _globalSettings = globalSettings;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
            _client = amazonSimpleEmailService;
            _source = $"\"{globalSettings.SiteName}\" <{globalSettings.Mail.ReplyToEmail}>";
            _senderTag = $"Server_{globalSettings.ProjectName?.Replace(' ', '_')}";
            if (!string.IsNullOrWhiteSpace(_globalSettings.Mail.AmazonConfigSetName))
            {
                _configSetName = _globalSettings.Mail.AmazonConfigSetName;
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public async Task SendEmailAsync(MailMessage message)
        {
            var request = new SendEmailRequest
            {
                ConfigurationSetName = _configSetName,
                Source = _source,
                Destination = MakeDestination(message),
                Message = new Message
                {
                    Subject = new Content(message.Subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = message.HtmlContent
                        },
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = message.TextContent
                        }
                    }
                },
                Tags = new List<MessageTag>
                {
                    new MessageTag { Name = "Environment", Value = _hostingEnvironment.EnvironmentName },
                    new MessageTag { Name = "Sender", Value = _senderTag }
                }
            };

            if (!string.IsNullOrWhiteSpace(message.Category))
            {
                request.Tags.Add(new MessageTag { Name = "Category", Value = message.Category });
            }

            try
            {
                await SendAsync(request, false);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to send email. Re-retying...");
                await SendAsync(request, true);
                throw e;
            }
        }

        public async Task SendBulkTemplatedEmailAsync<T>(string templateName, Dictionary<string, string> defaultModel,
            IEnumerable<(MailMessage message, T model)> templatedMessages)
        {
            var exceptions = new List<Exception>();
            foreach(var batch in BatchMessages(templatedMessages))
            {
                var destinations = batch.Select((templatedMessage) => new BulkEmailDestination
                {
                    Destination = MakeDestination(templatedMessage.message),
                    ReplacementTemplateData = JsonConvert.SerializeObject(templatedMessage.model),
                });
                var request = new SendBulkTemplatedEmailRequest
                {
                    ConfigurationSetName = _configSetName,
                    Source = _source,
                    Destinations = destinations.ToList(),
                    Template = templateName,
                    DefaultTemplateData = JsonConvert.SerializeObject(defaultModel),
                    DefaultTags = new List<MessageTag>
                    {
                        new MessageTag { Name = "Environment", Value = _hostingEnvironment.EnvironmentName },
                        new MessageTag { Name = "Sender", Value = _senderTag },
                    }
                };
                try
                {
                    await SendBulkEmails(request, false);
                }
                catch (Exception e)
                {
                    try
                    {
                        _logger.LogWarning(e, "Failed to send emails. Re-retying...");
                        await SendBulkEmails(request, true);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException("Exceptions thrown while sending bulk emails", exceptions);
            }
        }

        public async Task UpsertTemplate(string templateName, string subjectPart, string textPart = null, string htmlPart = null)
        {
            var template = new Template { TemplateName = templateName };
            if (subjectPart != null)
            {
                template.SubjectPart = subjectPart;
            }
            if (textPart != null)
            {
                template.TextPart = textPart;
            }
            if (htmlPart != null)
            {
                template.HtmlPart = htmlPart;
            }

            try
            {
                // Throws if not found
                var existingTemplate = await _client.GetTemplateAsync(new GetTemplateRequest
                {
                    TemplateName = templateName
                });

                await _client.UpdateTemplateAsync(new UpdateTemplateRequest
                {
                    Template = template
                });
            }
            catch (TemplateDoesNotExistException)
            {
                await _client.CreateTemplateAsync(new CreateTemplateRequest
                {
                    Template = template
                });
            }
        }

        private async Task SendAsync(SendEmailRequest request, bool retry)
        {
            if (retry)
            {
                // wait and try again
                await Task.Delay(2000);
            }
            await _client.SendEmailAsync(request);
        }

        private async Task SendBulkEmails(SendBulkTemplatedEmailRequest request, bool retry)
        {
            if (retry)
            {
                // wait and try again
                await Task.Delay(2000);
            }
            await _client.SendBulkTemplatedEmailAsync(request);
        }

        private Destination MakeDestination(MailMessage message)
        {
            var destination = new Destination
            {
                ToAddresses = message.ToEmails.ToList(),
            };
            if (message.BccEmails?.Any() ?? false)
            {
                destination.BccAddresses = message.BccEmails.ToList();
            }
            return destination;
        }

        private IEnumerable<IEnumerable<(MailMessage message, T model)>> BatchMessages<T>(
        IEnumerable<(MailMessage message, T model)> templatedMessages)
        {
            var batches = new List<IEnumerable<(MailMessage message, T model)>>();

            var batchRecipientCount = 0;
            var batch = new List<(MailMessage message, T model)>();
            foreach (var (message, model) in templatedMessages)
            {
                if (batches.Count == 0)
                {
                    batches.Add(batch);
                }

                var messageRecipientCount = message?.ToEmails?.Count() ?? 0 + message?.BccEmails?.Count() ?? 0;
                if (batchRecipientCount + messageRecipientCount < MAX_BATCH_MESSAGE_RECIPIENTS)
                {
                    batch.Add((message, model));
                    batchRecipientCount += messageRecipientCount;
                }
                else
                {
                    batches.Add(batch);
                    batch = new List<(MailMessage message, T model)>();
                    batchRecipientCount = 0;
                }
            }

            return batches;
        }
    }
}
