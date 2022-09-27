using System.Text;
using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Utilities;

namespace Bit.Core.Services;

public abstract class AzureQueueService<T>
{
    protected QueueClient _queueClient;
    protected JsonSerializerOptions _jsonOptions;

    protected AzureQueueService(QueueClient queueClient, JsonSerializerOptions jsonOptions)
    {
        _queueClient = queueClient;
        _jsonOptions = jsonOptions;
    }

    public async Task CreateManyAsync(IEnumerable<T> messages)
    {
        if (messages?.Any() != true)
        {
            return;
        }

        foreach (var json in SerializeMany(messages, _jsonOptions))
        {
            await _queueClient.SendMessageAsync(json);
        }
    }

    protected IEnumerable<string> SerializeMany(IEnumerable<T> messages, JsonSerializerOptions jsonOptions)
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
            JsonSerializer.Serialize(message, jsonOptions));

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
