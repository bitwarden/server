using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Syslog;

namespace Bit.Core.Utilities;

public static class LoggerFactoryExtensions
{
    public static void UseSerilog(
        this IApplicationBuilder appBuilder,
        IWebHostEnvironment env,
        IHostApplicationLifetime applicationLifetime,
        GlobalSettings globalSettings)
    {
        if (env.IsDevelopment() && !globalSettings.EnableDevLogging)
        {
            return;
        }

        applicationLifetime.ApplicationStopped.Register(Log.CloseAndFlush);
    }

    public static ILoggingBuilder AddSerilog(
        this ILoggingBuilder builder,
        WebHostBuilderContext context,
        Func<LogEvent, IGlobalSettings, bool> filter = null)
    {
        var globalSettings = new GlobalSettings();
        ConfigurationBinder.Bind(context.Configuration.GetSection("GlobalSettings"), globalSettings);

        if (context.HostingEnvironment.IsDevelopment() && !globalSettings.EnableDevLogging)
        {
            return builder;
        }

        bool inclusionPredicate(LogEvent e)
        {
            if (filter == null)
            {
                return true;
            }
            var eventId = e.Properties.ContainsKey("EventId") ? e.Properties["EventId"].ToString() : null;
            if (eventId?.Contains(Constants.BypassFiltersEventId.ToString()) ?? false)
            {
                return true;
            }
            return filter(e, globalSettings);
        }

        var config = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Filter.ByIncludingOnly(inclusionPredicate);

        if (CoreHelpers.SettingHasValue(globalSettings?.DocumentDb.Uri) &&
            CoreHelpers.SettingHasValue(globalSettings?.DocumentDb.Key))
        {
            config.WriteTo.AzureCosmosDB(new Uri(globalSettings.DocumentDb.Uri),
                globalSettings.DocumentDb.Key, timeToLive: TimeSpan.FromDays(7),
                partitionKey: "_partitionKey")
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Project", globalSettings.ProjectName);
        }
        else if (CoreHelpers.SettingHasValue(globalSettings?.Sentry.Dsn))
        {
            config.WriteTo.Sentry(globalSettings.Sentry.Dsn)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Project", globalSettings.ProjectName);
        }
        else if (CoreHelpers.SettingHasValue(globalSettings?.Syslog.Destination))
        {
            // appending sitename to project name to allow eaiser identification in syslog.
            var appName = $"{globalSettings.SiteName}-{globalSettings.ProjectName}";
            if (globalSettings.Syslog.Destination.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                config.WriteTo.LocalSyslog(appName);
            }
            else if (Uri.TryCreate(globalSettings.Syslog.Destination, UriKind.Absolute, out var syslogAddress))
            {
                // Syslog's standard port is 514 (both UDP and TCP). TLS does not have a standard port, so assume 514.
                int port = syslogAddress.Port >= 0
                    ? syslogAddress.Port
                    : 514;

                if (syslogAddress.Scheme.Equals("udp"))
                {
                    config.WriteTo.UdpSyslog(syslogAddress.Host, port, appName);
                }
                else if (syslogAddress.Scheme.Equals("tcp"))
                {
                    config.WriteTo.TcpSyslog(syslogAddress.Host, port, appName);
                }
                else if (syslogAddress.Scheme.Equals("tls"))
                {
                    // TLS v1.1, v1.2 and v1.3 are explicitly selected (leaving out TLS v1.0)
                    const SslProtocols protocols = SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;

                    if (CoreHelpers.SettingHasValue(globalSettings.Syslog.CertificateThumbprint))
                    {
                        config.WriteTo.TcpSyslog(syslogAddress.Host, port, appName,
                            secureProtocols: protocols,
                            certProvider: new CertificateStoreProvider(StoreName.My, StoreLocation.CurrentUser,
                                                                       globalSettings.Syslog.CertificateThumbprint));
                    }
                    else
                    {
                        config.WriteTo.TcpSyslog(syslogAddress.Host, port, appName,
                            secureProtocols: protocols,
                            certProvider: new CertificateFileProvider(globalSettings.Syslog.CertificatePath,
                                                                      globalSettings.Syslog?.CertificatePassword ?? string.Empty));
                    }

                }
            }
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.LogDirectory))
        {
            if (globalSettings.LogRollBySizeLimit.HasValue)
            {
                var pathFormat = Path.Combine(globalSettings.LogDirectory, $"{globalSettings.ProjectName.ToLowerInvariant()}.log");
                if (globalSettings.LogDirectoryByProject)
                {
                    pathFormat = Path.Combine(globalSettings.LogDirectory, globalSettings.ProjectName, "log.txt");
                }
                config.WriteTo.File(pathFormat, rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: globalSettings.LogRollBySizeLimit);
            }
            else
            {
                var pathFormat = Path.Combine(globalSettings.LogDirectory, $"{globalSettings.ProjectName.ToLowerInvariant()}_{{Date}}.log");
                if (globalSettings.LogDirectoryByProject)
                {
                    pathFormat = Path.Combine(globalSettings.LogDirectory, globalSettings.ProjectName, "{Date}.txt");
                }
                config.WriteTo.RollingFile(pathFormat);
            }
            config
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Project", globalSettings.ProjectName);
        }

        var serilog = config.CreateLogger();
        builder.AddSerilog(serilog);

        return builder;
    }
}
