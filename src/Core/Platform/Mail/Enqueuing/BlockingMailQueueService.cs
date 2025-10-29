using Bit.Core.Models.Mail;

namespace Bit.Core.Services;

public class BlockingMailEnqueuingService : IMailEnqueuingService
{
    public async Task EnqueueAsync(IMailQueueMessage message, Func<IMailQueueMessage, Task> fallback)
    {
        await fallback(message);
    }

    public async Task EnqueueManyAsync(IEnumerable<IMailQueueMessage> messages, Func<IMailQueueMessage, Task> fallback)
    {
        foreach (var message in messages)
        {
            await fallback(message);
        }
    }
}
