namespace Bit.Core.Models.Mail;

public interface IMailQueueMessage
{
    string Subject { get; set; }
    IEnumerable<string> ToEmails { get; set; }
    IEnumerable<string> BccEmails { get; set; }
    string Category { get; set; }
    string TemplateName { get; set; }
    object Model { get; set; }
}
