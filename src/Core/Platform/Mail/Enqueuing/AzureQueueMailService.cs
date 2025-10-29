using Azure.Storage.Queues;
using Bit.Core.Models.Mail;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.Services;

public class AzureQueueMailService : AzureQueueService<IMailQueueMessage>, IMailEnqueuingService
{
    public AzureQueueMailService(GlobalSettings globalSettings) : base(
        new QueueClient(globalSettings.Mail.ConnectionString, "mail"),
        JsonHelpers.IgnoreWritingNull)
    { }

    public Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback) =>
        CreateManyAsync(new[] { message });

    public Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback) =>
        CreateManyAsync(messages);
}
