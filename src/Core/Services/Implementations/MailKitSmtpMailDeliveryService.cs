using Bit.Core.Settings;
using Bit.Core.Utilities;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Bit.Core.Services;

public class MailKitSmtpMailDeliveryService : IMailDeliveryService
{
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<MailKitSmtpMailDeliveryService> _logger;
    private readonly string _replyDomain;
    private readonly string _replyEmail;

    public MailKitSmtpMailDeliveryService(
        GlobalSettings globalSettings,
        ILogger<MailKitSmtpMailDeliveryService> logger)
    {
        if (globalSettings.Mail?.Smtp?.Host == null)
        {
            throw new ArgumentNullException(nameof(globalSettings.Mail.Smtp.Host));
        }

        _replyEmail = CoreHelpers.PunyEncode(globalSettings.Mail?.ReplyToEmail);

        if (_replyEmail.Contains("@"))
        {
            _replyDomain = _replyEmail.Split('@')[1];
        }

        _globalSettings = globalSettings;
        _logger = logger;
    }

    public async Task SendEmailAsync(Models.Mail.MailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_globalSettings.SiteName, _replyEmail));
        mimeMessage.Subject = message.Subject;
        if (!string.IsNullOrWhiteSpace(_replyDomain))
        {
            mimeMessage.MessageId = $"<{Guid.NewGuid()}@{_replyDomain}>";
        }

        foreach (var address in message.ToEmails)
        {
            var punyencoded = CoreHelpers.PunyEncode(address);
            mimeMessage.To.Add(MailboxAddress.Parse(punyencoded));
        }

        if (message.BccEmails != null)
        {
            foreach (var address in message.BccEmails)
            {
                var punyencoded = CoreHelpers.PunyEncode(address);
                mimeMessage.Bcc.Add(MailboxAddress.Parse(punyencoded));
            }
        }

        var builder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(message.TextContent))
        {
            builder.TextBody = message.TextContent;
        }
        builder.HtmlBody = message.HtmlContent;
        mimeMessage.Body = builder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            if (_globalSettings.Mail.Smtp.TrustServer)
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }

            if (!_globalSettings.Mail.Smtp.StartTls && !_globalSettings.Mail.Smtp.Ssl &&
                _globalSettings.Mail.Smtp.Port == 25)
            {
                await client.ConnectAsync(_globalSettings.Mail.Smtp.Host, _globalSettings.Mail.Smtp.Port,
                    MailKit.Security.SecureSocketOptions.None);
            }
            else
            {
                var useSsl = _globalSettings.Mail.Smtp.Port == 587 && !_globalSettings.Mail.Smtp.SslOverride ?
                    false : _globalSettings.Mail.Smtp.Ssl;
                await client.ConnectAsync(_globalSettings.Mail.Smtp.Host, _globalSettings.Mail.Smtp.Port, useSsl);
            }

            if (CoreHelpers.SettingHasValue(_globalSettings.Mail.Smtp.Username) &&
                CoreHelpers.SettingHasValue(_globalSettings.Mail.Smtp.Password))
            {
                await client.AuthenticateAsync(_globalSettings.Mail.Smtp.Username,
                    _globalSettings.Mail.Smtp.Password);
            }

            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);
        }
    }
}
