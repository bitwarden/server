using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Data
{
    public class AzureQueueMessage<T> where T : class
    {
        public bool BlobBackedMessage => Message == null && Messages == null;
        public Guid MessageId { get; set; }
        public string BlobPath { get; set; } = null;
        public T Message { get; set; } = null;
        public IEnumerable<T> Messages { get; set; } = null;

        public AzureQueueMessage()
        {
            MessageId = Guid.NewGuid();
        }

        public AzureQueueMessage<T> ToBlobBackedMessage()
        {
            return new AzureQueueMessage<T>()
            {
                MessageId = MessageId
            };
        }
    }
}
