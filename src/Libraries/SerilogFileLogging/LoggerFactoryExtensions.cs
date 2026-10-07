using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

public static class LoggerFactoryExtensions
{
    /// <summary>
    /// Configures file logging via Serilog when not running in the Development environment.
    /// Reads configuration from <c>Logging:PathFormat</c> if present; otherwise falls back to
    /// the legacy <c>GlobalSettings:LogDirectory</c> / <c>LogDirectoryByProject</c> settings.
    /// </summary>
    /// <remarks>
    /// Only use this in services that run as part of a self-hosted Bitwarden Lite deployment.
    /// Cloud-hosted services should rely solely on the logging provided by the Bitwarden Server SDK.
    /// </remarks>
    public static IHostBuilder AddSerilogFileLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureLogging((context, logging) =>
        {
            if (context.HostingEnvironment.IsDevelopment())
            {
                return;
            }

            IConfiguration loggingConfiguration;

            // If they have begun using the new settings location, use that
            if (!string.IsNullOrEmpty(context.Configuration["Logging:PathFormat"]))
            {
                loggingConfiguration = context.Configuration.GetSection("Logging");
            }
            else
            {
                var globalSettingsSection = context.Configuration.GetSection("GlobalSettings");
                var loggingOptions = new LegacyFileLoggingOptions();
                globalSettingsSection.Bind(loggingOptions);

                if (string.IsNullOrWhiteSpace(loggingOptions.LogDirectory))
                {
                    return;
                }

                var projectName = loggingOptions.ProjectName
                    ?? context.HostingEnvironment.ApplicationName;

                string pathFormat;

                if (loggingOptions.LogRollBySizeLimit.HasValue)
                {
                    pathFormat = loggingOptions.LogDirectoryByProject
                        ? Path.Combine(loggingOptions.LogDirectory, projectName, "log.txt")
                        : Path.Combine(loggingOptions.LogDirectory, $"{projectName.ToLowerInvariant()}.log");
                }
                else
                {
                    pathFormat = loggingOptions.LogDirectoryByProject
                        ? Path.Combine(loggingOptions.LogDirectory, projectName, "{Date}.txt")
                        : Path.Combine(loggingOptions.LogDirectory, $"{projectName.ToLowerInvariant()}_{{Date}}.log");
                }

                // We want to rely on Serilog using the configuration section to have customization of the log levels
                // so we make a custom configuration source for them based on the legacy values and allow overrides from
                // the new location.
                loggingConfiguration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"PathFormat", pathFormat},
                        {"FileSizeLimitBytes", loggingOptions.LogRollBySizeLimit?.ToString(CultureInfo.InvariantCulture)}
                    })
                    .AddConfiguration(context.Configuration.GetSection("Logging"))
                    .Build();
            }

            logging.AddFile(loggingConfiguration);
        });
    }

    /// <summary>
    /// Our own proprietary options that we've always supported in `GlobalSettings` configuration section.
    /// </summary>
    private class LegacyFileLoggingOptions
    {
        public string? ProjectName { get; set; }
        public string? LogDirectory { get; set; } = "/etc/bitwarden/logs";
        public bool LogDirectoryByProject { get; set; } = true;
        public long? LogRollBySizeLimit { get; set; }
    }
}
