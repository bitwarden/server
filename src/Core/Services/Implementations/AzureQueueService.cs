using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public abstract class AzureQueueService<T>
    {
        protected QueueClient _queueClient;
        protected JsonSerializerSettings _jsonSettings;

        protected AzureQueueService(QueueClient queueClient, JsonSerializerSettings jsonSettings)
        {
            _queueClient = queueClient;
            _jsonSettings = jsonSettings;
        }

        public async Task CreateAsync(T message) => await CreateManyAsync(new[] { message });

        public async Task CreateManyAsync(IEnumerable<T> messages)
        {
            if (messages?.Any() != true)
            {
                return;
            }

            foreach (var json in SerializeMany(messages))
            {
                await _queueClient.SendMessageAsync(json);
            }
        }


        private IEnumerable<string> SerializeMany(IEnumerable<T> messages)
        {
            string SerializeMessage(T message) => JsonConvert.SerializeObject(message, _jsonSettings);

            var messagesLists = new List<List<T>> { new List<T>() };
            var strings = new List<string>();
            var ListMessageLength = 2; // to account for json array brackets "[]"
            foreach (var (message, jsonEvent) in messages.Select(m => (m, SerializeMessage(m))))
            {

                var messageLength = jsonEvent.Length + 1; // To account for json array comma
                if (ListMessageLength + messageLength > _queueClient.MessageMaxBytes)
                {
                    messagesLists.Add(new List<T> { message });
                    ListMessageLength = 2 + messageLength;
                }
                else
                {
                    messagesLists.Last().Add(message);
                    ListMessageLength += messageLength;
                }
            }
            return messagesLists.Select(l => JsonConvert.SerializeObject(l, _jsonSettings));
        }
    }
}
