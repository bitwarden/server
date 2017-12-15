using Microsoft.Azure.WebJobs;

namespace Bit.EventsProcessor
{
    public class Program
    {
        private static void Main()
        {
            var config = new JobHostConfiguration();
            if(config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            config.Queues.BatchSize = 5;

            var host = new JobHost(config);
            host.RunAndBlock();
        }
    }
}
