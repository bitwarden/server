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

                var serilog = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Filter.ByIncludingOnly(filter)
                    .WriteTo.AzureDocumentDB(new Uri(globalSettings.DocumentDb.Uri), globalSettings.DocumentDb.Key,
                        timeToLive: TimeSpan.FromDays(7))
                    .CreateLogger();

                factory.AddSerilog(serilog);
                appLifetime.ApplicationStopped.Register(Log.CloseAndFlush);
            }

            return factory;
        }
    }
}
