// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Mail;

public class MailQueueMessage : IMailQueueMessage
{
    public string Subject { get; set; }
    public IEnumerable<string> ToEmails { get; set; }
    public IEnumerable<string> BccEmails { get; set; }
    public string Category { get; set; }
    public string TemplateName { get; set; }

    [JsonConverter(typeof(HandlebarsObjectJsonConverter))]
    public object Model { get; set; }

    /// <summary>
    /// Indicates if this is an IMailer message (uses view rendering) vs HandlebarsMailService message (uses template rendering).
    /// True for IMailer messages, false or null for HandlebarsMailService messages.
    /// </summary>
    public bool IsMailerMessage { get; set; }

    /// <summary>
    /// Additional metadata for delivery (e.g., SendGridBypassListManagement).
    /// Used by both IMailer and HandlebarsMailService messages.
    /// </summary>
    public IDictionary<string, object> MetaData { get; set; }

    public MailQueueMessage() { }

    public MailQueueMessage(MailMessage message, string templateName, object model)
    {
        Subject = message.Subject;
        ToEmails = message.ToEmails;
        BccEmails = message.BccEmails;
        Category = string.IsNullOrEmpty(message.Category) ? templateName : message.Category;
        TemplateName = templateName;
        Model = model;
    }
}
