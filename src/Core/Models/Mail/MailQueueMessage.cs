using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Bit.Core.Models.Mail
{
    public class MailQueueMessage : IMailQueueMessage
    {
        public string Subject { get; set; }
        public IEnumerable<string> ToEmails { get; set; }
        public IEnumerable<string> BccEmails { get; set; }
        public string Category { get; set; }
        public string TemplateName { get; set; }
        private object _model;
        public object Model
        {
            get
            {
                if (_model?.GetType() == typeof(JObject))
                {
                    return (_model as JObject).ToObject(ModelType);
                }
                return _model;
            }

            set => _model = value;
        }
        public Type ModelType { get; set; }

        public MailQueueMessage() { }

        public MailQueueMessage(MailMessage message, string templateName, object model)
        {
            Subject = message.Subject;
            ToEmails = message.ToEmails;
            BccEmails = message.BccEmails;
            Category = string.IsNullOrEmpty(message.Category) ? templateName : message.Category;
            TemplateName = templateName;
            Model = model;
            ModelType = model.GetType();
        }
    }
}
