using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;

namespace Bit.Core.Utilities
{
    public static class LoggerFactoryExtensions
    {
        public static void UseSerilog(
            this IApplicationBuilder appBuilder,
            IHostingEnvironment env,
            IApplicationLifetime applicationLifetime,
            GlobalSettings globalSettings)
        {
            if(env.IsDevelopment())
            {
                return;
            }

            if(CoreHelpers.SettingHasValue(globalSettings?.Sentry.Dsn))
            {
                appBuilder.AddSentryContext();
            }
            applicationLifetime.ApplicationStopped.Register(Log.CloseAndFlush);
        }

        public static ILoggingBuilder AddSerilog(
            this ILoggingBuilder builder,
            WebHostBuilderContext context,
            Func<LogEvent, bool> filter = null)
        {
            if(context.HostingEnvironment.IsDevelopment())
            {
                return builder;
            }

            bool inclusionPredicate(LogEvent e)
            {
                if(filter == null)
                {
                    return true;
                }
                var eventId = e.Properties.ContainsKey("EventId") ? e.Properties["EventId"].ToString() : null;
                if(eventId?.Contains(Constants.BypassFiltersEventId.ToString()) ?? false)
                {
                    return true;
                }
                return filter(e);
            }

            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(context.Configuration.GetSection("GlobalSettings"), globalSettings);

            var config = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Filter.ByIncludingOnly(inclusionPredicate);

            if(CoreHelpers.SettingHasValue(globalSettings?.DocumentDb.Uri) &&
                CoreHelpers.SettingHasValue(globalSettings?.DocumentDb.Key))
            {
                config.WriteTo.AzureDocumentDB(new Uri(globalSettings.DocumentDb.Uri),
                    globalSettings.DocumentDb.Key, timeToLive: TimeSpan.FromDays(7))
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Project", globalSettings.ProjectName);
            }
            else if(CoreHelpers.SettingHasValue(globalSettings?.Sentry.Dsn))
            {
                config.WriteTo.Sentry(globalSettings.Sentry.Dsn)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Project", globalSettings.ProjectName)
                    .Destructure.With<HttpContextDestructingPolicy>()
                    .Filter.ByExcluding(e => e.Exception?.CheckIfCaptured() == true);
            }
            else if(CoreHelpers.SettingHasValue(globalSettings.LogDirectory))
            {
                if(globalSettings.LogRollBySizeLimit.HasValue)
                {
                    config.WriteTo.File($"{globalSettings.LogDirectory}/{globalSettings.ProjectName}/log.txt",
                        rollOnFileSizeLimit: true, fileSizeLimitBytes: globalSettings.LogRollBySizeLimit);
                }
                else
                {
                    config.WriteTo
                        .RollingFile($"{globalSettings.LogDirectory}/{globalSettings.ProjectName}/{{Date}}.txt");
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
}
