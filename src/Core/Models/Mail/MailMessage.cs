namespace Bit.Core.Models.Mail;

public class MailMessage
{
    public string Subject { get; set; }
    public IEnumerable<string> ToEmails { get; set; }
    public IEnumerable<string> BccEmails { get; set; }
    public string HtmlContent { get; set; }
    public string TextContent { get; set; }
    public string Category { get; set; }
    public IDictionary<string, object> MetaData { get; set; }
}
