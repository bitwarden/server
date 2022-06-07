using System.Threading.Tasks;
using AspNetCoreRateLimit;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Events;

namespace Bit.Identity
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webHost = CreateHostBuilder(args)
                .Build();

            using (var scope = webHost.Services.CreateScope())
            {
                var ipPolicyStore = scope.ServiceProvider.GetRequiredService<IIpPolicyStore>();

                await ipPolicyStore.SeedAsync();

                var clientPolicySTore = scope.ServiceProvider.GetRequiredService<IClientPolicyStore>();

                await clientPolicySTore.SeedAsync();
            }

            await webHost.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host
                .CreateDefaultBuilder(args)
                .ConfigureCustomAppConfiguration(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureLogging((hostingContext, logging) =>
                        logging.AddSerilog(hostingContext, e =>
                        {
                            var context = e.Properties["SourceContext"].ToString();
                            if (context.Contains(typeof(IpRateLimitMiddleware).FullName) &&
                                e.Level == LogEventLevel.Information)
                            {
                                return true;
                            }

                            if (context.Contains("IdentityServer4.Validation.TokenValidator") ||
                                context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                            {
                                return e.Level > LogEventLevel.Error;
                            }

                            return e.Level >= LogEventLevel.Error;
                        }));
                });
        }
    }
}
