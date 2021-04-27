using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class AzureQueueMailService : AzureQueueService<IMailQueueMessage>, IMailEnqueuingService
    {
        protected override QueueClient QueueClient { get; }
        protected override JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public AzureQueueMailService(
            GlobalSettings globalSettings)
        {
            QueueClient = new QueueClient(globalSettings.Mail.ConnectionString, "mail");
        }

        public Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback) =>
            CreateAsync(message);

        public Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback) =>
            CreateManyAsync(messages);
    }
}
