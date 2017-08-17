using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Jobs
{
    public class Program
    {
        private static ILicensingService _licensingService;

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseServer(new NoopServer())
                .Build();

            _licensingService = host.Services.GetRequiredService<ILicensingService>();

            MainAsync(args).Wait();
        }

        private async static Task MainAsync(string[] args)
        {
            if(args.Length == 0)
            {
                return;
            }

            switch(args[0])
            {
                case "validate-licenses":
                    await _licensingService.ValidateOrganizationsAsync();
                    break;
                case "refresh-licenses":
                    // TODO
                    break;
                default:
                    break;
            }
        }
    }
}
