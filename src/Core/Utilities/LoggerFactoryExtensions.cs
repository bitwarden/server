using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;

namespace Bit.Core.Utilities
{
    public static class LoggerFactoryExtensions
    {
        public static ILoggerFactory AddSerilog(
            this ILoggerFactory factory,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            Func<LogEvent, bool> filter = null)
        {
            if(!env.IsDevelopment())
            {
                if(filter == null)
                {
                    filter = (e) => true;
                }

                var config = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Filter.ByIncludingOnly(filter);

                if(globalSettings.DocumentDb != null && !string.IsNullOrWhiteSpace(globalSettings.DocumentDb.Uri) && 
                    !string.IsNullOrWhiteSpace(globalSettings.DocumentDb.Key))
                {
                    config.WriteTo.AzureDocumentDB(new Uri(globalSettings.DocumentDb.Uri), globalSettings.DocumentDb.Key,
                        timeToLive: TimeSpan.FromDays(7));
                }
                else
                {
                    // local file sink
                }

                var serilog = config.CreateLogger();
                factory.AddSerilog(serilog);
                appLifetime.ApplicationStopped.Register(Log.CloseAndFlush);
            }

            return factory;
        }
    }
}
