using Bit.Core.Models.Mail;

namespace Bit.Core.Services;

public interface IMailEnqueuingService
{
    Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback);
    Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback);
}
