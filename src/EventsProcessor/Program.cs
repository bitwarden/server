using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.EventsProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new HostBuilder();
            builder.ConfigureWebJobs(b =>
            {
                b.AddAzureStorageCoreServices();
                b.AddAzureStorage(a =>
                {
                    a.BatchSize = 5;
                });
                // Not working. ref: https://github.com/Azure/azure-webjobs-sdk/issues/1962
                b.AddDashboardLogging();
            });
            builder.ConfigureLogging((context, b) =>
            {
                b.AddConsole();
                b.SetMinimumLevel(LogLevel.Warning);
            });
            builder.ConfigureHostConfiguration(b =>
            {
                b.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                b.AddEnvironmentVariables();
            });
            var host = builder.Build();
            using(host)
            {
                host.Run();
            }
        }
    }
}
