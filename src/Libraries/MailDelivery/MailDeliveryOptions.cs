namespace Bit.Core.Platform.Mail.Delivery;

/// <summary>
/// Settings for delivering mail. The provider is chosen from which settings are configured:
/// <list type="number">
///   <item>Amazon SES and SendGrid both configured: SES, with <see cref="SendGridPercentage"/> of mail sent through SendGrid instead.</item>
///   <item>Amazon SES configured: SES.</item>
///   <item>An SMTP host configured: SMTP.</item>
///   <item>Otherwise mail is silently dropped.</item>
/// </list>
/// A value is treated as unset when it is null or whitespace.
/// </summary>
/// <remarks>
/// Property names mirror the legacy <c>globalSettings</c> configuration so it can be bound directly: most properties
/// match <c>globalSettings:mail</c>, <see cref="Amazon"/> matches <c>globalSettings:amazon</c>, and
/// <see cref="SiteName"/> and <see cref="ProjectName"/> match the root of <c>globalSettings</c>.
/// </remarks>
public sealed class MailDeliveryOptions
{
    /// <summary>
    /// Display name used in the <c>From</c> header.
    /// </summary>
    public string? SiteName { get; set; }

    /// <summary>
    /// Name of the sending service, attached to SES and SendGrid messages as a sender tag.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Address mail is sent from. Required by every provider except the no-op one.
    /// </summary>
    public string? ReplyToEmail { get; set; }

    /// <summary>
    /// SMTP server settings. Used when Amazon SES is not configured and <see cref="SmtpOptions.Host"/> is set.
    /// </summary>
    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>
    /// SendGrid API key. SendGrid is only used when Amazon SES is also configured; see <see cref="SendGridPercentage"/>.
    /// </summary>
    public string? SendGridApiKey { get; set; }

    /// <summary>
    /// Base URL of the SendGrid API.
    /// </summary>
    public string SendGridApiHost { get; set; } = "https://api.sendgrid.com";

    /// <summary>
    /// When Amazon SES is also configured, the percentage (0-99) of mail sent through SendGrid.
    /// Unset disables SendGrid in that mode.
    /// </summary>
    public int? SendGridPercentage { get; set; }

    /// <summary>
    /// Amazon credentials. Amazon SES is used when <see cref="AmazonOptions.AccessKeySecret"/> is set.
    /// </summary>
    public AmazonOptions Amazon { get; set; } = new();

    /// <summary>
    /// Amazon SES configuration set applied to every message, if set.
    /// </summary>
    public string? AmazonConfigSetName { get; set; }

    /// <summary>
    /// SMTP server settings.
    /// </summary>
    public sealed class SmtpOptions
    {
        /// <summary>
        /// SMTP server host name. Setting this selects SMTP when Amazon SES is not configured.
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// SMTP server port.
        /// </summary>
        public int Port { get; set; } = 25;

        /// <summary>
        /// Only matters on port 25, where a connection with neither this nor <see cref="Ssl"/> set is unencrypted.
        /// Otherwise, without SSL, STARTTLS is used whenever the server offers it.
        /// </summary>
        public bool StartTls { get; set; }

        /// <summary>
        /// Connect over SSL/TLS from the start. Ignored on port 587 unless <see cref="SslOverride"/> is set.
        /// </summary>
        public bool Ssl { get; set; }

        /// <summary>
        /// Honor <see cref="Ssl"/> on port 587, where SSL is otherwise disabled in favor of STARTTLS.
        /// </summary>
        public bool SslOverride { get; set; }

        /// <summary>
        /// SMTP username. The client authenticates only when both this and <see cref="Password"/> are set.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// SMTP password. The client authenticates only when both this and <see cref="Username"/> are set.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Accept the server certificate without validation.
        /// </summary>
        public bool TrustServer { get; set; }
    }

    /// <summary>
    /// Amazon credentials used for SES.
    /// </summary>
    public sealed class AmazonOptions
    {
        /// <summary>
        /// Amazon access key ID. Required when Amazon SES is used.
        /// </summary>
        public string? AccessKeyId { get; set; }

        /// <summary>
        /// Amazon secret access key. Setting this selects Amazon SES.
        /// </summary>
        public string? AccessKeySecret { get; set; }

        /// <summary>
        /// Amazon region system name, for example <c>us-east-1</c>. Required when Amazon SES is used.
        /// </summary>
        public string? Region { get; set; }
    }
}
