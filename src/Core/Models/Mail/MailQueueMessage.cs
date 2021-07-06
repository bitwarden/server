using System;
using System.Collections.Generic;
using Bit.Core.Utilities;
using Newtonsoft.Json;

namespace Bit.Core.Models.Mail
{
    public class MailQueueMessage : IMailQueueMessage
    {
        public Type ModelType => Model?.GetType();
        [JsonConverter(typeof(EncodedStringConverter))]
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

    public class MailQueueMessage<T> : IMailQueueMessage
    {
        [JsonConverter(typeof(EncodedStringConverter))]
        public string Subject { get; set; }
        public IEnumerable<string> ToEmails { get; set; }
        public IEnumerable<string> BccEmails { get; set; }
        public string Category { get; set; }
        public string TemplateName { get; set; }
        public T Model { get; set; }
        Type IMailQueueMessage.ModelType => typeof(T);
        object IMailQueueMessage.Model { get => Model; set => Model = (T)value; }

        public MailQueueMessage() { }

        public MailQueueMessage(MailMessage message, string templateName, T model)
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
