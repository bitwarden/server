using Bit.Core;
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

    [Fact]
    public void SelfHosted_WithoutOverride_SendsBetaChannelClosed()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;
        Assert.Equal("closed", Assert.Contains(FeatureFlagKeys.BetaChannelStatus, options.FlagValues));

        using var scope = provider.CreateScope();
        var all = scope.ServiceProvider.GetRequiredService<IFeatureService>().GetAll();
        Assert.Equal("closed", Assert.Contains(FeatureFlagKeys.BetaChannelStatus, all).GetValue<string>());
    }

    [Theory]
    [InlineData("Features:FlagValues:beta-channel-status")]
    [InlineData("GlobalSettings:LaunchDarkly:FlagValues:beta-channel-status")]
    public void SelfHosted_WithOverride_SendsTheOverride(string overrideKey)
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { overrideKey, "open" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;
        Assert.Equal("open", Assert.Contains(FeatureFlagKeys.BetaChannelStatus, options.FlagValues));

        using var scope = provider.CreateScope();
        var all = scope.ServiceProvider.GetRequiredService<IFeatureService>().GetAll();
        Assert.Equal("open", Assert.Contains(FeatureFlagKeys.BetaChannelStatus, all).GetValue<string>());
    }

    [Fact]
    public void Cloud_DoesNotGetTheSelfHostedBetaChannelDefault()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "false" },
            { "GlobalSettings:LaunchDarkly:SdkKey", "cloud-sdk-key" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;
        Assert.Equal("cloud-sdk-key", options.LaunchDarkly.SdkKey);
        Assert.DoesNotContain(FeatureFlagKeys.BetaChannelStatus, options.FlagValues.Keys);
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
