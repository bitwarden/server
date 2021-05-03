using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class AzureQueueMessage<T> where T : class
    {
        public bool BlobBackedMessage => Message == null && Messages == null;
        [JsonProperty]
        public Guid MessageId { get; private set; }
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
