using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AmazonSesMailDeliveryService : IMailDeliveryService, IDisposable
{
    private readonly GlobalSettings _globalSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<AmazonSesMailDeliveryService> _logger;
    private readonly IAmazonSimpleEmailService _client;
    private readonly string _source;
    private readonly string _senderTag;
    private readonly string _configSetName;

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

        var replyToEmail = CoreHelpers.PunyEncode(globalSettings.Mail.ReplyToEmail);

        _globalSettings = globalSettings;
        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _client = amazonSimpleEmailService;
        _source = $"\"{globalSettings.SiteName}\" <{replyToEmail}>";
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
            Destination = new Destination
            {
                ToAddresses = message.ToEmails
                    .Select(email => CoreHelpers.PunyEncode(email))
                    .ToList()
            },
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

        if (message.BccEmails?.Any() ?? false)
        {
            request.Destination.BccAddresses = message.BccEmails
                .Select(email => CoreHelpers.PunyEncode(email))
                .ToList();
        }

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
            _logger.LogWarning(e, "Failed to send email. Retrying...");
            await SendAsync(request, true);
            throw;
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
}
