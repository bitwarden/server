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

            var host = new JobHost(config);
            host.RunAndBlock();
        }
    }
}
