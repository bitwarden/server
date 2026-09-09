using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.GlobalSettingsBridge.Test;

public class ConfigureCorsOptionsTests
{
    [Theory]
    [InlineData("https://vault.example.com", true)]   // configured vault URI
    [InlineData("file://", true)]                     // Safari extension origin
    [InlineData("bw-desktop-file://bundle", true)]    // Desktop application protocol
    [InlineData("https://bitwarden.com", true)]       // Product website (cloud only)
    [InlineData("https://evil.example.com", false)]   // Unknown origin
    public void Configure_CloudInstance_AllowsExpectedOrigins(string origin, bool expected)
    {
        var policy = BuildDefaultPolicy(selfHosted: false, vaultUri: "https://vault.example.com");

        Assert.Equal(expected, policy.IsOriginAllowed(origin));
    }

    [Theory]
    [InlineData("https://vault.example.com", true)]   // configured vault URI
    [InlineData("file://", true)]                     // Safari extension origin
    [InlineData("bw-desktop-file://bundle", true)]    // Desktop application protocol
    [InlineData("https://bitwarden.com", false)]      // Product website blocked for self-host
    [InlineData("https://evil.example.com", false)]   // Unknown origin
    public void Configure_SelfHostedInstance_AllowsExpectedOrigins(string origin, bool expected)
    {
        var policy = BuildDefaultPolicy(selfHosted: true, vaultUri: "https://vault.example.com");

        Assert.Equal(expected, policy.IsOriginAllowed(origin));
    }

    [Fact]
    public void Configure_DefaultPolicy_AllowsAnyMethodHeaderAndCredentials()
    {
        var policy = BuildDefaultPolicy(selfHosted: false, vaultUri: "https://vault.example.com");

        Assert.True(policy.AllowAnyMethod);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.SupportsCredentials);
    }

    private static CorsPolicy BuildDefaultPolicy(bool selfHosted, string vaultUri)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["globalSettings:baseServiceUri:vault"] = vaultUri,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBitwardenEnvironment>(new TestBitwardenEnvironment
        {
            SelfHosted = selfHosted,
            SelfHostFlavor = selfHosted ? "unknown" : null,
        });
        services.AddGlobalSettingsBridge();
        var corsOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptions<CorsOptions>>().Value;
        return corsOptions.GetPolicy(corsOptions.DefaultPolicyName)!;
    }
}
