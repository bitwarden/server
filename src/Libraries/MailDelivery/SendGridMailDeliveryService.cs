using System.Globalization;
using Bit.Core.Models.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Bit.Core.Platform.Mail.Delivery;

internal class SendGridMailDeliveryService : IMailDeliveryService, IDisposable
{
    private readonly IHostEnvironment _hostingEnvironment;
    private readonly ILogger<SendGridMailDeliveryService> _logger;
    private readonly ISendGridClient _client;
    private readonly string? _siteName;
    private readonly string _senderTag;
    private readonly string? _replyToEmail;

    public SendGridMailDeliveryService(
        IOptions<MailDeliveryOptions> options,
        IHostEnvironment hostingEnvironment,
        ILogger<SendGridMailDeliveryService> logger)
        : this(new SendGridClient(options.Value.SendGridApiKey, options.Value.SendGridApiHost),
             options, hostingEnvironment, logger)
    {
    }

    public void Dispose()
    {
        // TODO: nothing to dispose
    }

    internal SendGridMailDeliveryService(
        ISendGridClient client,
        IOptions<MailDeliveryOptions> options,
        IHostEnvironment hostingEnvironment,
        ILogger<SendGridMailDeliveryService> logger)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SendGridApiKey))
        {
            throw new ArgumentNullException(nameof(settings.SendGridApiKey));
        }

        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _client = client;
        _siteName = settings.SiteName;
        _senderTag = $"Server_{settings.ProjectName?.Replace(' ', '_')}";
        _replyToEmail = PunyEncoding.PunyEncode(settings.ReplyToEmail);
    }

    public async Task SendEmailAsync(MailMessage message)
    {
        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(_replyToEmail, _siteName));
        msg.AddTos(message.ToEmails.Select(e => new EmailAddress(PunyEncoding.PunyEncode(e))).ToList());
        if (message.BccEmails?.Any() ?? false)
        {
            msg.AddBccs(message.BccEmails.Select(e => new EmailAddress(PunyEncoding.PunyEncode(e))).ToList());
        }

        if (!string.IsNullOrWhiteSpace(message.ReplyToAddress))
        {
            msg.SetReplyTo(new EmailAddress(PunyEncoding.PunyEncode(message.ReplyToAddress)));
        }

        msg.SetSubject(message.Subject);
        msg.AddContent(MimeType.Text, message.TextContent);
        msg.AddContent(MimeType.Html, message.HtmlContent);

        msg.AddCategory($"type:{message.Category}");
        msg.AddCategory($"env:{_hostingEnvironment.EnvironmentName}");
        msg.AddCategory($"sender:{_senderTag}");

        msg.SetClickTracking(false, false);
        msg.SetOpenTracking(false);

        if (message.MetaData.TryGetValue("SendGridBypassListManagement", out var sendGridBypassListManagement) &&
            Convert.ToBoolean(sendGridBypassListManagement, CultureInfo.InvariantCulture))
        {
            msg.SetBypassListManagement(true);
        }

        try
        {
            var success = await SendAsync(msg, false);
            if (!success)
            {
                _logger.LogWarning("Failed to send email. Retrying...");
                await SendAsync(msg, true);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to send email (with exception). Retrying...");
            await SendAsync(msg, true);
            throw;
        }
    }

    private async Task<bool> SendAsync(SendGridMessage message, bool retry)
    {
        if (retry)
        {
            // wait and try again
            await Task.Delay(2000);
        }

        var response = await _client.SendEmailAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid email sending failed with {0}: {1}", response.StatusCode, responseBody);
        }
        return response.IsSuccessStatusCode;
    }
}
