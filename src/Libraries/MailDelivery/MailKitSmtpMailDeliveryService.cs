using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bit.Core.Platform.Mail.Delivery;

internal class MailKitSmtpMailDeliveryService : IMailDeliveryService
{
    private readonly MailDeliveryOptions.SmtpOptions _smtp;
    private readonly string _smtpHost;
    private readonly string? _siteName;
    private readonly ILogger<MailKitSmtpMailDeliveryService> _logger;
    private readonly string? _replyDomain;
    private readonly string _replyEmail;

    public MailKitSmtpMailDeliveryService(
        IOptions<MailDeliveryOptions> options,
        ILogger<MailKitSmtpMailDeliveryService> logger)
    {
        var settings = options.Value;
        if (settings.Smtp.Host == null)
        {
            throw new ArgumentNullException(nameof(settings.Smtp.Host));
        }

        if (settings.ReplyToEmail == null)
        {
            throw new InvalidOperationException(
                $"{nameof(MailDeliveryOptions)}.{nameof(MailDeliveryOptions.ReplyToEmail)} is required to send mail over SMTP.");
        }

        _replyEmail = PunyEncoding.PunyEncode(settings.ReplyToEmail);

        if (_replyEmail.Contains('@'))
        {
            _replyDomain = _replyEmail.Split('@')[1];
        }

        _smtp = settings.Smtp;
        _smtpHost = settings.Smtp.Host;
        _siteName = settings.SiteName;
        _logger = logger;
    }

    public async Task SendEmailAsync(Models.Mail.MailMessage message)
        => await SendEmailAsync(message, CancellationToken.None);

    public async Task SendEmailAsync(Models.Mail.MailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_siteName, _replyEmail));
        mimeMessage.Subject = message.Subject;
        if (!string.IsNullOrWhiteSpace(_replyDomain))
        {
            mimeMessage.MessageId = $"<{Guid.NewGuid()}@{_replyDomain}>";
        }

        foreach (var address in message.ToEmails)
        {
            var punyencoded = PunyEncoding.PunyEncode(address);
            mimeMessage.To.Add(MailboxAddress.Parse(punyencoded));
        }

        if (message.BccEmails != null)
        {
            foreach (var address in message.BccEmails)
            {
                var punyencoded = PunyEncoding.PunyEncode(address);
                mimeMessage.Bcc.Add(MailboxAddress.Parse(punyencoded));
            }
        }

        if (!string.IsNullOrWhiteSpace(message.ReplyToAddress))
        {
            mimeMessage.ReplyTo.Add(MailboxAddress.Parse(PunyEncoding.PunyEncode(message.ReplyToAddress)));
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
            client.ServerCertificateValidationCallback = ValidateServerCertificate;

            if (!_smtp.StartTls && !_smtp.Ssl && _smtp.Port == 25)
            {
                await client.ConnectAsync(
                    _smtpHost,
                    _smtp.Port,
                    MailKit.Security.SecureSocketOptions.None,
                    cancellationToken
                );
            }
            else
            {
                var useSsl = _smtp.Port == 587 && !_smtp.SslOverride ? false : _smtp.Ssl;
                await client.ConnectAsync(
                    _smtpHost,
                    _smtp.Port,
                    useSsl,
                    cancellationToken
                );
            }

            if (!string.IsNullOrWhiteSpace(_smtp.Username) && !string.IsNullOrWhiteSpace(_smtp.Password))
            {
                await client.AuthenticateAsync(
                    _smtp.Username,
                    _smtp.Password,
                    cancellationToken
                );
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    internal bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (_smtp.TrustServer)
        {
            return true;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        // Allow CRL checks to fail open if unable to retrieve status.
        var hasOnlyCrlErrors = chain?.ChainStatus.All(status =>
            status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
            status.Status == X509ChainStatusFlags.OfflineRevocation) ?? false;
        if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && hasOnlyCrlErrors)
        {
            _logger.LogWarning("Certificate revocation status unavailable");
            return true;
        }

        return false;
    }
}
