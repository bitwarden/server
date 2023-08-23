using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

namespace Bit.Extensions.Hosting;

public static class HostBuilderExtensions
{
    /// <summary>
    ///
    /// </summary>
    public static IHostBuilder UseBitwardenDefaults(this IHostBuilder hostBuilder, Action<BitwardenHostOptions>? configure = null)
    {
        // We could default to not including logging in development environments like we currently do.
        var bitwardenHostOptions = new BitwardenHostOptions();
        configure?.Invoke(bitwardenHostOptions);
        return hostBuilder.UseBitwardenDefaults(bitwardenHostOptions);
    }

    public static IHostBuilder UseBitwardenDefaults(this IHostBuilder hostBuilder, BitwardenHostOptions bitwardenHostOptions)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddOptions<GlobalSettingsBase>()
                .Configure<IConfiguration>((options, config) =>
                {
                    options.SelfHosted = config.GetValue("globalSettings:selfHosted", false);
                });
        });

        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            if (context.Configuration.GetValue("globalSettings:selfHosted", false))
            {
                // Current ordering of Configuration:
                // 1. Chained (from Host config)
                //      1. Memory
                //      2. Memory
                //      3. Environment (DOTNET_)
                //      4. Chained
                //          1. Memory
                //          2. Environment (ASPNETCORE_)
                // 2. Json (appsettings.json)
                // 3. Json (appsettings.Environment.json)
                // 4. Secrets
                // 5. Environment (*)
                // 6. Command line args, if present
                // vv If selfhosted vv
                // 7. Json (appsettings.json) again
                // 8. Json (appsettings.Environment.json)
                // 9. Secrets (if development)
                // 10. Environment (*)
                // 11. Command line args, if present

                // As you can see there was a lot of doubling up,
                // I would rather insert the self hosted config, when necessary into
                // the index.

                // These would fail if two main things happen, the default host setup from .NET changes
                // and a new source is added before the appsettings ones.
                // or someone change the order or adding this helper, both things I believe would be quickly
                // discovered during development.

                // I expect the 3rd source to be the main appsettings.json file
                Debug.Assert(builder.Sources[2] is FileConfigurationSource mainJsonSource
                    && mainJsonSource.Path == "appsettings.json");
                // I expect the 4th source to be the environment specific json file
                Debug.Assert(builder.Sources[3] is FileConfigurationSource environmentJsonSource
                    && environmentJsonSource.Path == $"appsettings.{context.HostingEnvironment.EnvironmentName}.json");

                // If both of those are true, I feel good about inserting our own self hosted config after
                builder.Sources.Insert(4, new JsonConfigurationSource
                {
                    Path = "appsettings.SelfHosted.json",
                    Optional = true,
                    ReloadOnChange = true,
                });
            }
        });

        if (bitwardenHostOptions.IncludeLogging)
        {
            hostBuilder.UseSerilog((context, builder) =>
            {
                builder.Enrich.WithProperty("Project", context.HostingEnvironment.ApplicationName);
                // We should still default to using logger.BeginScope();
                builder.Enrich.FromLogContext();
                builder.ReadFrom.Configuration(context.Configuration);
            });
        }

        if (bitwardenHostOptions.IncludeMetrics)
        {
            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddOpenTelemetry()
                    .WithMetrics(options =>
                        options.AddOtlpExporter())
                    .WithTracing(options =>
                        options.AddOtlpExporter());
            });
        }

        return hostBuilder;
    }
}
