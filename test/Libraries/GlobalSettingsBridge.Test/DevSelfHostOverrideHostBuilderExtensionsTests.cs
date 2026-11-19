using Bitwarden.Server.Sdk.Environment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bit.GlobalSettingsBridge.Test;

public class DevSelfHostOverrideHostBuilderExtensionsTests
{
    [Fact]
    public void ConfigureDevSelfHostOverride_DevelopmentWithDevelopSelfHosted_AppliesOverride()
    {
        using var host = BuildHost("Development", new()
        {
            ["GlobalSettings:SiteName"] = "base",
            ["GlobalSettings:ProjectName"] = "base-project",
            ["developSelfHosted"] = "true",
            ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
        });

        var config = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("override", config["GlobalSettings:SiteName"]);
        Assert.Equal("base-project", config["GlobalSettings:ProjectName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_NotDevelopment_IgnoresOverride()
    {
        using var host = BuildHost("Production", new()
        {
            ["GlobalSettings:SiteName"] = "base",
            ["developSelfHosted"] = "true",
            ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
        });

        Assert.Equal("base", host.Services.GetRequiredService<IConfiguration>()["GlobalSettings:SiteName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_DevelopmentWithoutDevelopSelfHosted_IgnoresOverride()
    {
        using var host = BuildHost("Development", new()
        {
            ["GlobalSettings:SiteName"] = "base",
            ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
        });

        Assert.Equal("base", host.Services.GetRequiredService<IConfiguration>()["GlobalSettings:SiteName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_MapsNestedKeys()
    {
        using var host = BuildHost("Development", new()
        {
            ["GlobalSettings:BaseServiceUri:Vault"] = "https://vault.example.com",
            ["developSelfHosted"] = "true",
            ["Dev:SelfHostOverride:GlobalSettings:BaseServiceUri:Vault"] = "https://vault.selfhost.example.com",
        });

        Assert.Equal("https://vault.selfhost.example.com",
            host.Services.GetRequiredService<IConfiguration>()["GlobalSettings:BaseServiceUri:Vault"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_OnlyMapsGlobalSettingsOverrideSection()
    {
        using var host = BuildHost("Development", new()
        {
            ["developSelfHosted"] = "true",
            ["Dev:SelfHostOverride:Other:Key"] = "other",
            ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
        });

        var config = host.Services.GetRequiredService<IConfiguration>();
        Assert.Null(config["GlobalSettings:Other:Key"]);
        Assert.Null(config["Other:Key"]);
        Assert.Equal("other", config["Dev:SelfHostOverride:Other:Key"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_WinsOverEarlierSources()
    {
        using var host = new HostBuilder()
            .UseEnvironment("Development")
            .ConfigureAppConfiguration(config => config
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["developSelfHosted"] = "true",
                    ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
                })
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GlobalSettings:SiteName"] = "later-source",
                }))
            .ConfigureDevSelfHostOverride()
            .Build();

        Assert.Equal("override", host.Services.GetRequiredService<IConfiguration>()["GlobalSettings:SiteName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_SourcesAddedAfterwardWin()
    {
        using var host = new HostBuilder()
            .UseEnvironment("Development")
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["developSelfHosted"] = "true",
                ["Dev:SelfHostOverride:GlobalSettings:SiteName"] = "override",
            }))
            .ConfigureDevSelfHostOverride()
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GlobalSettings:SiteName"] = "added-afterward",
            }))
            .Build();

        Assert.Equal("added-afterward", host.Services.GetRequiredService<IConfiguration>()["GlobalSettings:SiteName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_PrecedingSourceReloads_UpdatesOverride()
    {
        var reloadable = new ReloadableConfigurationSource();
        reloadable.Provider.Set("developSelfHosted", "true");
        reloadable.Provider.Set("Dev:SelfHostOverride:GlobalSettings:SiteName", "first");

        using var host = new HostBuilder()
            .UseEnvironment("Development")
            .ConfigureAppConfiguration(config => config.Add(reloadable))
            .ConfigureDevSelfHostOverride()
            .Build();

        var config = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("first", config["GlobalSettings:SiteName"]);

        reloadable.Provider.SetAndReload("Dev:SelfHostOverride:GlobalSettings:SiteName", "second");
        Assert.Equal("second", config["GlobalSettings:SiteName"]);

        reloadable.Provider.SetAndReload("developSelfHosted", "false");
        Assert.Null(config["GlobalSettings:SiteName"]);
    }

    [Fact]
    public void ConfigureDevSelfHostOverride_BitwardenEnvironmentSeesOverride()
    {
        using var host = new HostBuilder()
            .UseEnvironment("Development")
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["globalSettings:selfHosted"] = "false",
                ["developSelfHosted"] = "true",
                ["Dev:SelfHostOverride:GlobalSettings:selfHosted"] = "true",
            }))
            .ConfigureDevSelfHostOverride()
            .ConfigureServices(services => services
                .AddBitwardenEnvironment()
                .AddGlobalSettingsBridge())
            .Build();

        Assert.True(host.Services.GetRequiredService<IBitwardenEnvironment>().SelfHosted);
    }

    private static IHost BuildHost(string environmentName, Dictionary<string, string?> values)
    {
        return new HostBuilder()
            .UseEnvironment(environmentName)
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(values))
            .ConfigureDevSelfHostOverride()
            .Build();
    }

    private sealed class ReloadableConfigurationSource : IConfigurationSource
    {
        public ReloadableConfigurationProvider Provider { get; } = new();

        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
    }

    private sealed class ReloadableConfigurationProvider : ConfigurationProvider
    {
        public void SetAndReload(string key, string? value)
        {
            Set(key, value);
            OnReload();
        }
    }
}
