using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Jobs
{
    public class Program
    {
        private static ILicensingService _licensingService;
        private static ILogger<Program> _logger;

        public static void Main(string[] args)
        {
            var parameters = ParseParameters(args);
            try
            {
                var host = new WebHostBuilder()
                    .UseContentRoot(parameters.ContainsKey("d") ? parameters["d"] : Directory.GetCurrentDirectory())
                    .UseStartup<Startup>()
                    .UseServer(new NoopServer())
                    .Build();

                _logger = host.Services.GetRequiredService<ILogger<Program>>();
                _licensingService = host.Services.GetRequiredService<ILicensingService>();
            }
            catch(Exception e)
            {
                if(_logger != null)
                {
                    _logger.LogCritical(1, e, "Error while bootstrapping.");
                }
                throw e;
            }

            MainAsync(parameters).Wait();
        }

        private async static Task MainAsync(IDictionary<string, string> parameters)
        {
            if(!parameters.ContainsKey("j"))
            {
                return;
            }

            _logger.LogInformation("Starting job '{0}'.", parameters["j"]);

            try
            {
                switch(parameters["j"])
                {
                    case "validate-licenses":
                        await _licensingService.ValidateOrganizationsAsync();
                        break;
                    case "refresh-licenses":
                        // TODO
                        break;
                    case "alive":
                        _logger.LogInformation(DateTime.UtcNow.ToString());
                        break;
                    default:
                        break;
                }
            }
            catch(Exception e)
            {
                _logger.LogError(2, e, "Error performing job.");
                throw e;
            }
        }

        private static IDictionary<string, string> ParseParameters(string[] args)
        {
            var dict = new Dictionary<string, string>();
            for(var i = 0; i < args.Length; i = i + 2)
            {
                if(!args[i].StartsWith("-"))
                {
                    continue;
                }

                dict.Add(args[i].Substring(1), args[i + 1]);
            }

            return dict;
        }
    }
}
