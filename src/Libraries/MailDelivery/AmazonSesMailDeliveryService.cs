using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Bit.Core.Models.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

internal class AmazonSesMailDeliveryService : IMailDeliveryService, IDisposable
{
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly ILogger<AmazonSesMailDeliveryService> _logger;
    private readonly IAmazonSimpleEmailService _client;
    private readonly string _source;
    private readonly string _senderTag;
    private readonly string? _configSetName;

    public AmazonSesMailDeliveryService(
        IOptions<MailDeliveryOptions> options,
        IHostEnvironment hostingEnvironment,
        ILogger<AmazonSesMailDeliveryService> logger)
    : this(options, hostingEnvironment, logger,
          new AmazonSimpleEmailServiceClient(
            options.Value.Amazon.AccessKeyId,
            options.Value.Amazon.AccessKeySecret,
            RegionEndpoint.GetBySystemName(options.Value.Amazon.Region))
          )
    {
    }

    internal AmazonSesMailDeliveryService(
        IOptions<MailDeliveryOptions> options,
        IHostEnvironment hostingEnvironment,
        ILogger<AmazonSesMailDeliveryService> logger,
        IAmazonSimpleEmailService amazonSimpleEmailService)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Amazon.AccessKeyId))
        {
            throw new ArgumentNullException(nameof(settings.Amazon.AccessKeyId));
        }
        if (string.IsNullOrWhiteSpace(settings.Amazon.AccessKeySecret))
        {
            throw new ArgumentNullException(nameof(settings.Amazon.AccessKeySecret));
        }
        if (string.IsNullOrWhiteSpace(settings.Amazon.Region))
        {
            throw new ArgumentNullException(nameof(settings.Amazon.Region));
        }

        var replyToEmail = PunyEncoding.PunyEncode(settings.ReplyToEmail);

        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _client = amazonSimpleEmailService;
        _source = $"\"{settings.SiteName}\" <{replyToEmail}>";
        _senderTag = $"Server_{settings.ProjectName?.Replace(' ', '_')}";
        if (!string.IsNullOrWhiteSpace(settings.AmazonConfigSetName))
        {
            _configSetName = settings.AmazonConfigSetName;
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
                    .Select(email => PunyEncoding.PunyEncode(email))
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
                .Select(email => PunyEncoding.PunyEncode(email))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(message.ReplyToAddress))
        {
            request.ReplyToAddresses = [PunyEncoding.PunyEncode(message.ReplyToAddress)];
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
