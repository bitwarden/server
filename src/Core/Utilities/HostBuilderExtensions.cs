using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Bit.Core.Utilities;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureCustomAppConfiguration(this IHostBuilder hostBuilder, string[] args)
    {
        // Reload app configuration with SelfHosted overrides.
        return hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            if (Environment.GetEnvironmentVariable("globalSettings__selfHosted")?.ToLower() != "true")
            {
                return;
            }

            var env = hostingContext.HostingEnvironment;

            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.SelfHosted.json", optional: true, reloadOnChange: true);

            if (env.IsDevelopment())
            {
                var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                if (appAssembly != null)
                {
                    config.AddUserSecrets(appAssembly, optional: true);
                }
            }

            config.AddEnvironmentVariables();

            if (args != null)
            {
                config.AddCommandLine(args);
            }
        });
    }
}
