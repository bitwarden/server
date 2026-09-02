using Bit.SharedWeb.Utilities;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bit.SharedWeb.Test.Utilities;

public class ServerSdkCompatibilityExtensionsTests
{
    [Fact]
    public void NewConfigLocationArePreferred()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { "Features:LaunchDarkly:SdkKey", "primary" },
            { "Features:FlagValues:Flag", "new-value" },
            { "Features:FlagValues:OtherFlag", "hi!" },

            { "GlobalSettings:LaunchDarkly:SdkKey", "secondary" },
            { "GlobalSettings:LaunchDarkly:FlagValues:Flag", "old-value" },
            { "GlobalSettings:LaunchDarkly:FlagValues:AnotherFlag", "hello!" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

        Assert.Equal("primary", options.LaunchDarkly.SdkKey);
        Assert.NotEmpty(options.KnownFlags);
        Assert.Equal("new-value", Assert.Contains("Flag", options.FlagValues));
        Assert.Equal("hi!", Assert.Contains("OtherFlag", options.FlagValues));
        Assert.Equal("hello!", Assert.Contains("AnotherFlag", options.FlagValues));
    }

    [Fact]
    public void OnlyGlobalSettingsSdkKeySet()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:LaunchDarkly:SdkKey", "original" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

        Assert.Equal("original", options.LaunchDarkly.SdkKey);
    }

    [Fact]
    public void FlagDataFilePath_PointsToExistingFile_LogsWarning()
    {
        var flagFile = Path.Combine(Path.GetTempPath(), $"flags-{Guid.NewGuid():N}.json");
        File.WriteAllText(flagFile, "{}");

        try
        {
            using var fakeProvider = new FakeLoggerProvider();
            using var provider = CreateProvider(
                new Dictionary<string, string?>
                {
                    { "GlobalSettings:LaunchDarkly:FlagDataFilePath", flagFile },
                },
                services => services.AddLogging(b => b.AddProvider(fakeProvider)));

            _ = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

            var warning = Assert.Single(
                fakeProvider.Collector.GetSnapshot(),
                record => record.Level == LogLevel.Warning);
            Assert.Contains(flagFile, warning.Message);
        }
        finally
        {
            File.Delete(flagFile);
        }
    }

    [Fact]
    public void FlagDataFilePath_FileMissing_DoesNotLog()
    {
        using var fakeProvider = new FakeLoggerProvider();
        using var provider = CreateProvider(
            new Dictionary<string, string?>
            {
                { "GlobalSettings:LaunchDarkly:FlagDataFilePath", "/does/not/exist.json" },
            },
            services => services.AddLogging(b => b.AddProvider(fakeProvider)));

        _ = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

        Assert.DoesNotContain(
            fakeProvider.Collector.GetSnapshot(),
            record => record.Level == LogLevel.Warning);
    }

    private static ServiceProvider CreateProvider(
        Dictionary<string, string?> config,
        Action<IServiceCollection>? extraServices = null)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var environment = Substitute.For<IHostEnvironment>();
        environment.ApplicationName = "SharedWeb";

        services.AddSingleton(environment);
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddFeatureFlagServices();
        services.ApplyServerCompatibilityLayer();

        extraServices?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
