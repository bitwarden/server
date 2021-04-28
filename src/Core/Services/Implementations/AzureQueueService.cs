using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using IdentityServer4.Extensions;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public abstract class AzureQueueService<T>
    {
        protected abstract QueueClient QueueClient { get; }
        protected abstract JsonSerializerSettings JsonSettings { get; }

        public async Task CreateAsync(T message)
        {
            var json = JsonConvert.SerializeObject(message, JsonSettings);
            await QueueClient.SendMessageAsync(json);
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

            foreach (var json in SerializeMany(messages, JsonSettings))
            {
                await QueueClient.SendMessageAsync(json);
            }
        }


        protected IEnumerable<string> SerializeMany(IEnumerable<T> messages, JsonSerializerSettings jsonSettings)
        {
            var messagesLists = new List<List<T>> { new List<T>() };
            var strings = new List<string>();
            var ListMessageLength = 2; // to account for json array brackets "[]"
            foreach (var (message, jsonEvent) in messages.Select(e => (e, JsonConvert.SerializeObject(e, jsonSettings))))
            {

                var messageLength = jsonEvent.Length + 1; // To account for json array comma
                if (ListMessageLength + messageLength > QueueClient.MessageMaxBytes)
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
            return messagesLists.Select(l => JsonConvert.SerializeObject(l, jsonSettings));
        }
    }
}
