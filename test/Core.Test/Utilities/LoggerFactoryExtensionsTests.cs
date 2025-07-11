using System.Reflection;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class LoggerFactoryExtensionsTests
{
    [Fact]
    public void AddSerilog_IsDevelopment_AddsNoProviders()
    {
        var providers = GetProviders([], "Development");

        Assert.Empty(providers);
    }

    [Fact]
    public void AddSerilog_IsDevelopment_DevLoggingEnabled_AddsSerilog()
    {
        var providers = GetProviders(new Dictionary<string, string?>
        {
            { "GlobalSettings:EnableDevLogging", "true" },
        }, "Development");

        var provider = Assert.Single(providers);
        var serilog = Assert.IsAssignableFrom<SerilogLoggerProvider>(provider);
        var details = GetSerilogDetails(serilog);
    }

    private Logger GetSerilogDetails(SerilogLoggerProvider provider)
    {
        var fieldInfo = typeof(SerilogLoggerProvider).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("We tried to private reflection to get the serilog logger but it's not there. It may have moved.");

        if (fieldInfo.GetValue(provider) is not Serilog.Core.Logger logger)
        {
            throw new Exception("The private field _logger was there but it's either null or not assignable to Serilog.Core.Logger");
        }

        return logger;
    }

    private static IEnumerable<ILoggerProvider> GetProviders(Dictionary<string, string?> initialData, string environment = "Production")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        var hostingEnvironment = Substitute.For<IWebHostEnvironment>();

        hostingEnvironment
            .EnvironmentName
            .Returns(environment);

        var context = new WebHostBuilderContext
        {
            HostingEnvironment = hostingEnvironment,
            Configuration = config,
        };

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(context);
        });

        var provider = services.BuildServiceProvider();
        return provider.GetServices<ILoggerProvider>();
    }
}
