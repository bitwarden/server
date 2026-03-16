using Bit.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public void AddSerilog_IsProduction_AddsSerilog()
    {
        var providers = GetProviders([]);

        var provider = Assert.Single(providers);
        Assert.IsAssignableFrom<SerilogLoggerProvider>(provider);
    }

    [Fact]
    public async Task AddSerilog_FileLogging_Old_Works()
    {
        var tempDir = Directory.CreateTempSubdirectory();

        var providers = GetProviders(new Dictionary<string, string?>
        {
            { "GlobalSettings:ProjectName", "Test" },
            { "GlobalSettings:LogDirectoryByProject", "true" },
            { "GlobalSettings:LogDirectory", tempDir.FullName },
        });

        var provider = Assert.Single(providers);
        Assert.IsAssignableFrom<SerilogLoggerProvider>(provider);

        var logger = provider.CreateLogger("Test");
        logger.LogWarning("This is a test");

        provider.Dispose();

        var logFile = Assert.Single(tempDir.EnumerateFiles("Test/*.txt"));

        var logFileContents = await File.ReadAllTextAsync(logFile.FullName);

        Assert.Contains(
            "This is a test",
            logFileContents
        );
    }

    [Fact]
    public async Task AddSerilog_FileLogging_New_Works()
    {
        var tempDir = Directory.CreateTempSubdirectory();

        var provider = GetServiceProvider(new Dictionary<string, string?>
        {
            { "Logging:PathFormat", $"{tempDir}/Logs/log-{{Date}}.log" },
        }, "Production");

        var logger = provider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Test");

        logger.LogWarning("This is a test");

        await provider.DisposeAsync();

        var logFile = Assert.Single(tempDir.EnumerateFiles("Logs/*.log"));

        var logFileContents = await File.ReadAllTextAsync(logFile.FullName);

        Assert.DoesNotContain(
            "This configuration location for file logging has been deprecated.",
            logFileContents
        );
        Assert.Contains(
            "This is a test",
            logFileContents
        );
    }

    [Fact]
    public async Task AddSerilogFileLogging_LegacyConfig_WithLevelCustomization_InfoLogs_DoNotFillUpFile()
    {
        await AssertSmallFileAsync((tempDir, config) =>
        {
            config["GlobalSettings:LogDirectory"] = tempDir;
            config["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning";
        });
    }

    [Fact]
    public async Task AddSerilogFileLogging_NewConfig_WithLevelCustomization_InfoLogs_DoNotFillUpFile()
    {
        await AssertSmallFileAsync((tempDir, config) =>
        {
            config["Logging:PathFormat"] = Path.Combine(tempDir, "log.txt");
            config["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning";
        });
    }

    private static async Task AssertSmallFileAsync(Action<string, Dictionary<string, string?>> configure)
    {
        using var tempDir = new TempDirectory();
        var config = new Dictionary<string, string?>();

        configure(tempDir.Directory, config);

        var provider = GetServiceProvider(config, "Production");

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var microsoftLogger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Testing");

        for (var i = 0; i < 100; i++)
        {
            microsoftLogger.LogInformation("Tons of useless information");
        }

        var otherLogger = loggerFactory.CreateLogger("Bitwarden");

        for (var i = 0; i < 5; i++)
        {
            otherLogger.LogInformation("Mildly more useful information but not as frequent.");
        }

        await provider.DisposeAsync();

        var logFiles = Directory.EnumerateFiles(tempDir.Directory, "*.txt", SearchOption.AllDirectories);
        var logFile = Assert.Single(logFiles);

        using var fr = File.OpenRead(logFile);
        Assert.InRange(fr.Length, 0, 1024);
    }

    private static IEnumerable<ILoggerProvider> GetProviders(Dictionary<string, string?> initialData, string environment = "Production")
    {
        var provider = GetServiceProvider(initialData, environment);
        return provider.GetServices<ILoggerProvider>();
    }

    private static ServiceProvider GetServiceProvider(Dictionary<string, string?> initialData, string environment)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        var hostingEnvironment = Substitute.For<IHostEnvironment>();

        hostingEnvironment
            .EnvironmentName
            .Returns(environment);

        var context = new HostBuilderContext(new Dictionary<object, object>())
        {
            HostingEnvironment = hostingEnvironment,
            Configuration = config,
        };

        var services = new ServiceCollection();

        var hostBuilder = Substitute.For<IHostBuilder>();
        hostBuilder
            .When(h => h.ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>()))
            .Do(call =>
            {
                var configureAction = call.Arg<Action<HostBuilderContext, IServiceCollection>>();
                configureAction(context, services);
            });

        hostBuilder.AddSerilogFileLogging();

        hostBuilder
            .ConfigureServices(Arg.Any<Action<HostBuilderContext, IServiceCollection>>())
            .Received(1);

        return services.BuildServiceProvider();
    }
}
