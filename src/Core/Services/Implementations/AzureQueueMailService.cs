using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Data;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueMailService : IMailEnqueuingService
    {
        public const string QueueMessageContainerName = "queue-messages";
        private readonly QueueClient _queueClient;
        private readonly CloudBlobClient _blobClient;
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        private  CloudBlobContainer _queueMessageContainer;

        public AzureQueueMailService(
            GlobalSettings globalSettings)
        {
            _queueClient = new QueueClient(globalSettings.Mail.ConnectionString, "mail");
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Mail.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback)
        {
            await SendMessageAsync(new AzureQueueMessage<IMailQueueMessage>
            {
                Message = message
            });
        }

        public async Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback)
        {
            await SendMessageAsync(new AzureQueueMessage<IMailQueueMessage>
            {
                Messages = messages
            });
        }

        private async Task SendMessageAsync(AzureQueueMessage<IMailQueueMessage> message)
        {
            var json = JsonConvert.SerializeObject(message, _jsonSettings);
            if (json.Length > _queueClient.MessageMaxBytes)
            {
                await InitAsync();
                var blob = _queueMessageContainer.GetBlockBlobReference($"{message.MessageId}");
                await blob.UploadTextAsync(json);

                json = JsonConvert.SerializeObject(message.ToBlobBackedMessage(), _jsonSettings);
            }
            await _queueClient.SendMessageAsync(json);
        }

        private async Task InitAsync()
        {
            if (_queueMessageContainer == null)
            {
                _queueMessageContainer = _blobClient.GetContainerReference(QueueMessageContainerName);
                await _queueMessageContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null);
            }
        }
    }
}
