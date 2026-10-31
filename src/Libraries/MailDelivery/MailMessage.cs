namespace Bit.Core.Models.Mail;

/// <summary>
/// A fully rendered email, ready to be handed to an <see cref="Platform.Mail.Delivery.IMailDeliveryService"/>.
/// </summary>
public class MailMessage
{
    /// <summary>
    /// Subject line.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Recipient addresses.
    /// </summary>
    public IEnumerable<string> ToEmails { get; set; } = [];

    /// <summary>
    /// Blind-copy recipient addresses, if any.
    /// </summary>
    public IEnumerable<string>? BccEmails { get; set; }

    /// <summary>
    /// <c>Reply-To</c> header; omitted when null or whitespace. The <c>From</c> address always comes from
    /// <see cref="Platform.Mail.Delivery.MailDeliveryOptions.ReplyToEmail"/>.
    /// </summary>
    public string? ReplyToAddress { get; set; }

    /// <summary>
    /// HTML body.
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>
    /// Plain-text body.
    /// </summary>
    public string? TextContent { get; set; }

    /// <summary>
    /// An optional category for processing at the upstream email delivery service.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Provider-specific delivery hints. Currently only <c>SendGridBypassListManagement</c> is recognized.
    /// </summary>
    public IDictionary<string, object> MetaData { get; set; } = new Dictionary<string, object>();
}
