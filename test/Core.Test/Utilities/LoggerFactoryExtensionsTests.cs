using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
