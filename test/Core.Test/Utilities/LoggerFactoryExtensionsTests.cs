using System.Net;
using System.Net.Sockets;
using System.Text;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
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
        Assert.IsAssignableFrom<SerilogLoggerProvider>(provider);
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
            { "GlobalSetting:LogDirectoryByProject", "true" },
            { "GlobalSettings:LogDirectory", tempDir.FullName },
        });

        var provider = Assert.Single(providers);
        Assert.IsAssignableFrom<SerilogLoggerProvider>(provider);

        var logger = provider.CreateLogger("Test");
        logger.LogWarning("This is a test");

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

        // Writing to the file is buffered, give it a little time to flush
        await Task.Delay(5);

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
    public async Task AddSerilog_SyslogConfigured_Warns()
    {
        // Setup a fake syslog server
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 25000);
        listener.Start();

        var provider = GetServiceProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:SysLog:Destination", "tcp://127.0.0.1:25000" },
            { "GlobalSettings:SiteName", "TestSite" },
            { "GlobalSettings:ProjectName", "TestProject" },
        }, "Production");

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Test");

        logger.LogWarning("This is a test");

        // Look in syslog for data
        using var socket = await listener.AcceptSocketAsync(cts.Token);

        // This is rather lazy as opposed to implementing smarter syslog message
        // reading but thats not what this test about, so instead just give
        // the sink time to finish its work in the background
        await Task.Delay(5);

        List<string> messages = [];

        while (true)
        {
            var buffer = new byte[1024];
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token);

            if (received == 0)
            {
                break;
            }

            var response = Encoding.ASCII.GetString(buffer, 0, received);
            messages.Add(response);

            if (messages.Count == 2)
            {
                break;
            }
        }

        Assert.Collection(
            messages,
            (firstMessage) => Assert.Contains("Syslog for logging has been deprecated", firstMessage),
            (secondMessage) => Assert.Contains("This is a test", secondMessage)
        );
    }

    private static IEnumerable<ILoggerProvider> GetProviders(Dictionary<string, string?> initialData, string environment = "Production")
    {
        var provider = GetServiceProvider(initialData, environment);
        return provider.GetServices<ILoggerProvider>();
    }

    private static IServiceProvider GetServiceProvider(Dictionary<string, string?> initialData, string environment)
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

        return services.BuildServiceProvider();
    }
}
