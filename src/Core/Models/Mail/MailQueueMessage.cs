using System.Collections.Generic;

namespace Bit.Core.Models.Mail
{
    public class MailQueueMessage : IMailQueueMessage
    {
        public string Subject { get; set; }
        public IEnumerable<string> ToEmails { get; set; }
        public IEnumerable<string> BccEmails { get; set; }
        public string Category { get; set; }
        public string TemplateName { get; set; }
        public object Model { get; set; }

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
}
