using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public async Task CreateAsync(T message)
        {
            var json = JsonConvert.SerializeObject(message, _jsonSettings);
            await _queueClient.SendMessageAsync(CoreHelpers.Base64EncodeString(json));
        }

        public async Task CreateManyAsync(IEnumerable<T> messages)
        {
            if (messages?.Any() != true)
            {
                return;
            }

            if (!messages.Skip(1).Any())
            {
                await CreateAsync(messages.First());
                return;
            }

            foreach (var b64Json in SerializeManyToB64(messages, _jsonSettings))
            {
                await _queueClient.SendMessageAsync(b64Json);
            }
        }

        protected IEnumerable<string> SerializeManyToB64(IEnumerable<T> messages, JsonSerializerSettings jsonSettings)
        {
            var messagesLists = new List<List<T>> { new List<T>() };
            var strings = new List<string>();
            var ListMessageByteLength = 2; // to account for json array brackets "[]"
            foreach (var (message, jsonEvent) in messages.Select(e => (e, JsonConvert.SerializeObject(e, jsonSettings))))
            {
                var messageByteLength = ByteLength(jsonEvent) + 1; // To account for json array comma
                if (B64EncodedLength(ListMessageByteLength + messageByteLength) > _queueClient.MessageMaxBytes)
                {
                    messagesLists.Add(new List<T> { message });
                    ListMessageByteLength = 2 + messageByteLength;
                }
                else
                {
                    messagesLists.Last().Add(message);
                    ListMessageByteLength += messageByteLength;
                }
            }
            return messagesLists.Select(l => CoreHelpers.Base64EncodeString(JsonConvert.SerializeObject(l, jsonSettings)));
        }

        private int ByteLength(string s) => Encoding.UTF8.GetByteCount(s);
        private int B64EncodedLength(int byteLength) => 4 * (int)Math.Ceiling((double)byteLength / 3);
    }
}
