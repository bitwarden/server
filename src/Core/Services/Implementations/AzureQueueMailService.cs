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
        public AzureQueueMailService(GlobalSettings globalSettings) : base(
            new QueueClient(globalSettings.Mail.ConnectionString, "mail"),
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
        { }

        public Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback) =>
            CreateAsync(message);

        public Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback) =>
            CreateManyAsync(messages);
    }
}
