using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

public static class LoggerFactoryExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <returns></returns>
    public static IHostBuilder AddSerilogFileLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureLogging((context, logging) =>
        {
            if (context.HostingEnvironment.IsDevelopment())
            {
                return;
            }

            // If they have begun using the new settings location, use that
            if (!string.IsNullOrEmpty(context.Configuration["Logging:PathFormat"]))
            {
                logging.AddFile(context.Configuration.GetSection("Logging"));
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

                if (loggingOptions.LogRollBySizeLimit.HasValue)
                {
                    var pathFormat = loggingOptions.LogDirectoryByProject
                        ? Path.Combine(loggingOptions.LogDirectory, projectName, "log.txt")
                        : Path.Combine(loggingOptions.LogDirectory, $"{projectName.ToLowerInvariant()}.log");

                    logging.AddFile(
                        pathFormat: pathFormat,
                        fileSizeLimitBytes: loggingOptions.LogRollBySizeLimit.Value
                    );
                }
                else
                {
                    var pathFormat = loggingOptions.LogDirectoryByProject
                        ? Path.Combine(loggingOptions.LogDirectory, projectName, "{Date}.txt")
                        : Path.Combine(loggingOptions.LogDirectory, $"{projectName.ToLowerInvariant()}_{{Date}}.log");

                    logging.AddFile(
                        pathFormat: pathFormat
                    );
                }
            }
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
