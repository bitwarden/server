using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var builder = new WebHostBuilder()
                .UseConfiguration(config)
                .UseKestrel()
                .UseStartup<Startup>();

            var contentRoot = config.GetValue<string>("contentRoot");
            if(string.IsNullOrWhiteSpace(contentRoot))
            {
                builder.UseContentRoot(contentRoot);
            }

            var webRoot = config.GetValue<string>("webRoot");
            if(string.IsNullOrWhiteSpace(webRoot))
            {
                builder.UseWebRoot(webRoot);
            }

            var host = builder.Build();
            host.Run();
        }
    }
}
