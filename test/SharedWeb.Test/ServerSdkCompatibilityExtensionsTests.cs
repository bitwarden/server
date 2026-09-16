using System.Text.Json;
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
    public void PamFlags_NothingConfigured_DefaultToOn()
    {
        using var provider = CreateProvider([]);

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

        Assert.Equal("true", Assert.Contains(FeatureFlagKeys.Pam, options.FlagValues));
        Assert.Equal("true", Assert.Contains(FeatureFlagKeys.PM28191_CipherAdminOpsToSdk, options.FlagValues));
    }

    [Fact]
    public void PamFlags_ConfiguredValueWinsOverTheBranchDefault()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { $"Features:FlagValues:{FeatureFlagKeys.Pam}", "false" },
            { $"GlobalSettings:LaunchDarkly:FlagValues:{FeatureFlagKeys.PM28191_CipherAdminOpsToSdk}", "false" },
        });

        var options = provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value;

        Assert.Equal("false", Assert.Contains(FeatureFlagKeys.Pam, options.FlagValues));
        Assert.Equal("false", Assert.Contains(FeatureFlagKeys.PM28191_CipherAdminOpsToSdk, options.FlagValues));
    }

    [Fact]
    public void PamFlags_NoSdkKey_ResolveEnabled()
    {
        using var provider = CreateProvider([]);
        using var scope = provider.CreateScope();

        var featureService = scope.ServiceProvider.GetRequiredService<IFeatureService>();

        Assert.True(featureService.IsEnabled(FeatureFlagKeys.Pam));
        Assert.True(featureService.IsEnabled(FeatureFlagKeys.PM28191_CipherAdminOpsToSdk));
    }

    [Fact]
    public void Vfo1Foundation_PinnedOff_EvenWhenConfiguredOn()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { $"Features:FlagValues:{FeatureFlagKeys.VFO1Foundation}", "true" },
        });
        using var scope = provider.CreateScope();

        var featureService = scope.ServiceProvider.GetRequiredService<IFeatureService>();

        Assert.False(featureService.IsEnabled(FeatureFlagKeys.VFO1Foundation));
        Assert.True(featureService.IsEnabled(FeatureFlagKeys.Pam));

        // The clients fall back to their own default for an omitted flag, so /config has to
        // state the pinned flag outright rather than leave it out.
        var all = featureService.GetAll();
        Assert.True(all.TryGetValue(FeatureFlagKeys.VFO1Foundation, out var pinnedValue));
        Assert.Equal(JsonValueKind.False, pinnedValue!.GetValueKind());
    }

    [Fact]
    public void Pam_PinnedOn_EvenWhenConfiguredOff()
    {
        // The FlagValues default only feeds the data source when no SdkKey is set, so it never
        // reaches a LaunchDarkly-connected environment - UAT reported pm-37044-pam-v-0 as false
        // with the default in place. Only the pin gets there, so it has to beat a value too.
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            { $"Features:FlagValues:{FeatureFlagKeys.Pam}", "false" },
        });
        using var scope = provider.CreateScope();

        var featureService = scope.ServiceProvider.GetRequiredService<IFeatureService>();

        Assert.True(featureService.IsEnabled(FeatureFlagKeys.Pam));

        var all = featureService.GetAll();
        Assert.True(all.TryGetValue(FeatureFlagKeys.Pam, out var pinnedValue));
        Assert.Equal(JsonValueKind.True, pinnedValue!.GetValueKind());
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
