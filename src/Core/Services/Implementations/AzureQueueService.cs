using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public abstract class AzureQueueService
    {
        private const int _maxMessageBody = 64000; // 64 MB

        protected IEnumerable<string> SerializeMany<T>(IEnumerable<T> messages, JsonSerializerSettings jsonSettings)
        {
            var messagesLists = new List<List<T>> { new List<T>() };
            var strings = new List<string>();
            var ListMessageLength = 2; // to account for json array brackets "[]"
            foreach (var (message, jsonEvent) in messages.Select(e => (e, JsonConvert.SerializeObject(e, jsonSettings))))
            {

                var messageLength = jsonEvent.Length + 1; // To account for json array comma
                if (ListMessageLength + messageLength > _maxMessageBody)
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
