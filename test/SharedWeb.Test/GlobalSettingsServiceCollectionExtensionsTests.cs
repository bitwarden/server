using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Bit.SharedWeb.Test.Utilities;

public class GlobalSettingsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGlobalSettings_RegistersGlobalSettingsAndIGlobalSettingsAsSameSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration());
        services.AddSingleton(BuildEnvironment("Production"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<GlobalSettings>();
        var iface = provider.GetRequiredService<IGlobalSettings>();

        Assert.Same(concrete, iface);
    }

    [Fact]
    public void AddGlobalSettings_BindsGlobalSettingsConfigurationSection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "bound-site" },
        }));
        services.AddSingleton(BuildEnvironment("Production"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        Assert.Equal("bound-site", provider.GetRequiredService<GlobalSettings>().SiteName);
    }

    [Fact]
    public void AddGlobalSettings_DoesNotOverrideExistingRegistration()
    {
        var preExisting = new GlobalSettings { SiteName = "pre-existing" };
        var services = new ServiceCollection();
        services.AddSingleton(preExisting);
        services.AddSingleton<IConfiguration>(BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "should-not-bind" },
        }));
        services.AddSingleton(BuildEnvironment("Production"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        Assert.Same(preExisting, provider.GetRequiredService<GlobalSettings>());
        Assert.Equal("pre-existing", provider.GetRequiredService<GlobalSettings>().SiteName);
    }

    [Fact]
    public void AddGlobalSettings_DevelopmentWithDevelopSelfHosted_AppliesSelfHostOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "base" },
            { "GlobalSettings:ProjectName", "base-project" },
            { "developSelfHosted", "true" },
            { "Dev:SelfHostOverride:GlobalSettings:SiteName", "override" },
        }));
        services.AddSingleton(BuildEnvironment("Development"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<GlobalSettings>();
        Assert.Equal("override", settings.SiteName);
        Assert.Equal("base-project", settings.ProjectName);
    }

    [Fact]
    public void AddGlobalSettings_NotDevelopment_IgnoresSelfHostOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "base" },
            { "developSelfHosted", "true" },
            { "Dev:SelfHostOverride:GlobalSettings:SiteName", "override" },
        }));
        services.AddSingleton(BuildEnvironment("Production"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        Assert.Equal("base", provider.GetRequiredService<GlobalSettings>().SiteName);
    }

    [Fact]
    public void AddGlobalSettings_DevelopmentWithoutDevelopSelfHostedFlag_IgnoresSelfHostOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "base" },
            { "Dev:SelfHostOverride:GlobalSettings:SiteName", "override" },
        }));
        services.AddSingleton(BuildEnvironment("Development"));

        services.AddGlobalSettings();

        using var provider = services.BuildServiceProvider();
        Assert.Equal("base", provider.GetRequiredService<GlobalSettings>().SiteName);
    }

    [Fact]
    public void AddGlobalSettingsServices_BindsConfigurationAndExposesAsSingletonInstance()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new()
        {
            { "GlobalSettings:SiteName", "bound-site" },
        });
        var environment = BuildEnvironment("Production");

        var returned = services.AddGlobalSettingsServices(configuration, environment);

        Assert.Equal("bound-site", returned.SiteName);

        using var provider = services.BuildServiceProvider();
        Assert.Same(returned, provider.GetRequiredService<GlobalSettings>());
        Assert.Same(returned, provider.GetRequiredService<IGlobalSettings>());
    }

    [Fact]
    public void AddGlobalSettingsServices_WhenAlreadyRegisteredAsInstance_ReturnsExistingWithoutReBinding()
    {
        var preExisting = new GlobalSettings { SiteName = "pre-existing" };
        var services = new ServiceCollection();
        services.AddSingleton(preExisting);

        var returned = services.AddGlobalSettingsServices(
            BuildConfiguration(new() { { "GlobalSettings:SiteName", "should-not-bind" } }),
            BuildEnvironment("Production"));

        Assert.Same(preExisting, returned);
        Assert.Equal("pre-existing", returned.SiteName);
    }

    [Fact]
    public void AddGlobalSettingsServices_WhenAlreadyRegisteredAsFactory_RemovesAndRebindsAsInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new GlobalSettings { SiteName = "from-factory" });

        var returned = services.AddGlobalSettingsServices(
            BuildConfiguration(new() { { "GlobalSettings:SiteName", "bound-site" } }),
            BuildEnvironment("Production"));

        Assert.Equal("bound-site", returned.SiteName);

        using var provider = services.BuildServiceProvider();
        Assert.Same(returned, provider.GetRequiredService<GlobalSettings>());
    }

    [Fact]
    public void AddGlobalSettingsServices_DevelopmentWithDevelopSelfHosted_AppliesSelfHostOverride()
    {
        var services = new ServiceCollection();

        var returned = services.AddGlobalSettingsServices(
            BuildConfiguration(new()
            {
                { "GlobalSettings:SiteName", "base" },
                { "developSelfHosted", "true" },
                { "Dev:SelfHostOverride:GlobalSettings:SiteName", "override" },
            }),
            BuildEnvironment("Development"));

        Assert.Equal("override", returned.SiteName);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }

    private static IHostEnvironment BuildEnvironment(string environmentName)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName = environmentName;
        return env;
    }
}
