using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Bit.Core.Services;

public class SendGridMailDeliveryService : IMailDeliveryService, IDisposable
{
    private readonly GlobalSettings _globalSettings;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<SendGridMailDeliveryService> _logger;
    private readonly ISendGridClient _client;
    private readonly string _senderTag;
    private readonly string _replyToEmail;

    public SendGridMailDeliveryService(
        GlobalSettings globalSettings,
        IWebHostEnvironment hostingEnvironment,
        ILogger<SendGridMailDeliveryService> logger)
        : this(new SendGridClient(globalSettings.Mail.SendGridApiKey),
             globalSettings, hostingEnvironment, logger)
    {
    }

    public void Dispose()
    {
        // TODO: nothing to dispose
    }

    public SendGridMailDeliveryService(
        ISendGridClient client,
        GlobalSettings globalSettings,
        IWebHostEnvironment hostingEnvironment,
        ILogger<SendGridMailDeliveryService> logger)
    {
        if (string.IsNullOrWhiteSpace(globalSettings.Mail?.SendGridApiKey))
        {
            throw new ArgumentNullException(nameof(globalSettings.Mail.SendGridApiKey));
        }

        _globalSettings = globalSettings;
        _hostingEnvironment = hostingEnvironment;
        _logger = logger;
        _client = client;
        _senderTag = $"Server_{globalSettings.ProjectName?.Replace(' ', '_')}";
        _replyToEmail = CoreHelpers.PunyEncode(globalSettings.Mail.ReplyToEmail);
    }

    public async Task SendEmailAsync(MailMessage message)
    {
        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(_replyToEmail, _globalSettings.SiteName));
        msg.AddTos(message.ToEmails.Select(e => new EmailAddress(CoreHelpers.PunyEncode(e))).ToList());
        if (message.BccEmails?.Any() ?? false)
        {
            msg.AddBccs(message.BccEmails.Select(e => new EmailAddress(CoreHelpers.PunyEncode(e))).ToList());
        }

        msg.SetSubject(message.Subject);
        msg.AddContent(MimeType.Text, message.TextContent);
        msg.AddContent(MimeType.Html, message.HtmlContent);

        msg.AddCategory($"type:{message.Category}");
        msg.AddCategory($"env:{_hostingEnvironment.EnvironmentName}");
        msg.AddCategory($"sender:{_senderTag}");

        msg.SetClickTracking(false, false);
        msg.SetOpenTracking(false);

        if (message.MetaData != null &&
            message.MetaData.ContainsKey("SendGridBypassListManagement") &&
            Convert.ToBoolean(message.MetaData["SendGridBypassListManagement"]))
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
