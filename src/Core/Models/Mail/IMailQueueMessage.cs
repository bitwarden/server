namespace Bit.Core.Models.Mail;

public interface IMailQueueMessage
{
    string Subject { get; set; }
    IEnumerable<string> ToEmails { get; set; }
    IEnumerable<string> BccEmails { get; set; }
    string Category { get; set; }
    string TemplateName { get; set; }
    object Model { get; set; }

    /// <summary>
    /// Indicates if this is an IMailer message (uses view rendering) vs HandlebarsMailService message (uses template rendering).
    /// </summary>
    public bool IsMailerMessage { get; set; }

    /// <summary>
    /// Additional metadata for delivery (e.g., SendGridBypassListManagement).
    /// </summary>
    public IDictionary<string, object>? MetaData { get; set; }
}
