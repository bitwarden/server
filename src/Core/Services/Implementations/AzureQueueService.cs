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
            var base64 = CoreHelpers.Base64EncodeString(json);
            await _queueClient.SendMessageAsync(base64);
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

            foreach (var json in SerializeMany(messages, _jsonSettings))
            {
                await _queueClient.SendMessageAsync(json);
            }
        }

        protected IEnumerable<string> SerializeMany(IEnumerable<T> messages, JsonSerializerSettings jsonSettings)
        {
            // Calculate Base-64 encoded text with padding
            int getBase64Size(int byteCount) => ((4 * byteCount / 3) + 3) & ~3;

            var messagesList = new List<string>();
            var messagesListSize = 0;
            
            int calculateByteSize(int totalSize, int toAdd) =>
                // Calculate the total length this would be w/ "[]" and commas
                getBase64Size(totalSize + toAdd + messagesList.Count + 2);

            // Format the final array string, i.e. [{...},{...}]
            string getArrayString()
            {
                if (messagesList.Count == 1)
                {
                    return CoreHelpers.Base64EncodeString(messagesList[0]);
                }
                return CoreHelpers.Base64EncodeString(
                    string.Concat("[", string.Join(',', messagesList), "]"));
            }
            
            var serializedMessages = messages.Select(message =>
                JsonConvert.SerializeObject(message, jsonSettings));

            foreach (var message in serializedMessages)
            {
                var messageSize = Encoding.UTF8.GetByteCount(message);
                if (calculateByteSize(messagesListSize, messageSize) > _queueClient.MessageMaxBytes)
                {
                    yield return getArrayString();
                    messagesListSize = 0;
                    messagesList.Clear();
                }

                messagesList.Add(message);
                messagesListSize += messageSize;
            }

            if (messagesList.Any())
            {
                yield return getArrayString();
            }
        }
    }
}
