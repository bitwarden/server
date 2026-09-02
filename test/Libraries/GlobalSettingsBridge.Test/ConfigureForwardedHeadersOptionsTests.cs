using System.Net;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using IPNetwork = System.Net.IPNetwork;

namespace Bit.GlobalSettingsBridge.Test;

public class ConfigureForwardedHeadersOptionsTests
{
    [Fact]
    public void Configure_AlwaysSets_XForwardedForAndProtoHeaders()
    {
        var options = Build([]);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
    }

    [Fact]
    public void Configure_ParsesKnownProxiesFromConfig()
    {
        var options = Build(new()
        {
            ["globalSettings:knownProxies"] = "10.0.0.1,192.168.1.1",
        });

        Assert.Contains(IPAddress.Parse("10.0.0.1"), options.KnownProxies);
        Assert.Contains(IPAddress.Parse("192.168.1.1"), options.KnownProxies);
    }

    [Fact]
    public void Configure_ParsesKnownNetworksFromConfig()
    {
        var options = Build(new()
        {
            ["globalSettings:knownNetworks"] = "10.0.0.0/8,192.168.0.0/16",
        });

        Assert.Contains(IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
        Assert.Contains(IPNetwork.Parse("192.168.0.0/16"), options.KnownIPNetworks);
    }

    [Fact]
    public void Configure_NullsForwardLimit_WhenMultipleKnownProxies()
    {
        var options = Build(new()
        {
            ["globalSettings:knownProxies"] = "10.0.0.1,10.0.0.2",
        });

        Assert.Null(options.ForwardLimit);
    }

    [Fact]
    public void Configure_NullsForwardLimit_WhenMultipleKnownNetworks()
    {
        var options = Build(new()
        {
            ["globalSettings:knownNetworks"] = "10.0.0.0/8,192.168.0.0/16",
        });

        Assert.Null(options.ForwardLimit);
    }

    [Fact]
    public void Configure_IgnoresInvalidProxyAddresses()
    {
        var options = Build(new()
        {
            ["globalSettings:knownProxies"] = "not-an-ip,10.0.0.1",
        });

        Assert.Contains(IPAddress.Parse("10.0.0.1"), options.KnownProxies);
        Assert.DoesNotContain(options.KnownProxies, ip => ip.ToString() == "not-an-ip");
    }

    [Fact]
    public void Configure_IgnoresInvalidNetworkAddresses()
    {
        var options = Build(new()
        {
            ["globalSettings:knownNetworks"] = "not-a-network,10.0.0.0/8",
        });

        Assert.Contains(IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
        Assert.DoesNotContain(options.KnownIPNetworks, n => n.ToString() == "not-a-network");
    }

    [Fact]
    public void Configure_TrimsWhitespaceFromProxiesAndNetworks()
    {
        var options = Build(new()
        {
            ["globalSettings:knownProxies"] = " 10.0.0.1 , 192.168.1.1 ",
            ["globalSettings:knownNetworks"] = " 10.0.0.0/8 , 192.168.0.0/16 ",
        });

        Assert.Contains(IPAddress.Parse("10.0.0.1"), options.KnownProxies);
        Assert.Contains(IPAddress.Parse("192.168.1.1"), options.KnownProxies);
        Assert.Contains(IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
        Assert.Contains(IPNetwork.Parse("192.168.0.0/16"), options.KnownIPNetworks);
    }

    [Fact]
    public void Configure_NonLiteFlavor_StillAppliesConfigProxies()
    {
        // Non-lite flavor attempts a nginx DNS lookup (which fails in test environments)
        // and falls through silently. Config-specified proxies must still be applied.
        var options = BuildWithFlavor("unknown", new()
        {
            ["globalSettings:knownProxies"] = "10.0.0.1",
        });

        Assert.Contains(IPAddress.Parse("10.0.0.1"), options.KnownProxies);
    }

    // Builds options with a "lite" flavor so the nginx DNS attempt is skipped, keeping
    // tests deterministic regardless of whether an nginx container is reachable.
    private static ForwardedHeadersOptions Build(Dictionary<string, string?> configValues)
        => BuildWithFlavor("lite", configValues);

    private static ForwardedHeadersOptions BuildWithFlavor(string flavor, Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBitwardenEnvironment>(new TestBitwardenEnvironment
        {
            SelfHosted = true,
            SelfHostFlavor = flavor,
        });
        services.AddGlobalSettingsBridge();
        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
