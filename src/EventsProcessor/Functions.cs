using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Bit.EventsProcessor
{
    public class Functions
    {
        public async static Task ProcessQueueMessageAsync(
            [QueueTrigger("event")] string message, TextWriter logger, CancellationToken token)
        {
            await logger.WriteLineAsync(message);
        }
    }
}
