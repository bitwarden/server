using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.AdminConsole.EventIntegrations;

public class EventIntegrationServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly GlobalSettings _globalSettings;

    public EventIntegrationServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();
        _globalSettings = CreateGlobalSettings([]);

        // Add required infrastructure services
        _services.TryAddSingleton(_globalSettings);
        _services.TryAddSingleton<IGlobalSettings>(_globalSettings);
        _services.AddLogging();

        // Mock Redis connection for cache
        _services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        // Mock required repository dependencies for commands
        _services.TryAddScoped(_ => Substitute.For<IOrganizationIntegrationRepository>());
        _services.TryAddScoped(_ => Substitute.For<IOrganizationIntegrationConfigurationRepository>());
        _services.TryAddScoped(_ => Substitute.For<IOrganizationRepository>());
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_RegistersAllServices()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        using var provider = _services.BuildServiceProvider();

        var cache = provider.GetRequiredKeyedService<IFusionCache>(EventIntegrationsCacheConstants.CacheName);
        Assert.NotNull(cache);

        var validator = provider.GetRequiredService<IOrganizationIntegrationConfigurationValidator>();
        Assert.NotNull(validator);

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetService<ICreateOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IUpdateOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IDeleteOrganizationIntegrationCommand>());
        Assert.NotNull(sp.GetService<IGetOrganizationIntegrationsQuery>());

        Assert.NotNull(sp.GetService<ICreateOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IUpdateOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IDeleteOrganizationIntegrationConfigurationCommand>());
        Assert.NotNull(sp.GetService<IGetOrganizationIntegrationConfigurationsQuery>());
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries_AreRegisteredAsScoped()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var createIntegrationDescriptor = _services.First(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationCommand));
        var createConfigDescriptor = _services.First(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand));

        Assert.Equal(ServiceLifetime.Scoped, createIntegrationDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, createConfigDescriptor.Lifetime);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries_DifferentInstancesPerScope()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var provider = _services.BuildServiceProvider();

        ICreateOrganizationIntegrationConfigurationCommand? instance1, instance2, instance3;
        using (var scope1 = provider.CreateScope())
        {
            instance1 = scope1.ServiceProvider.GetService<ICreateOrganizationIntegrationConfigurationCommand>();
        }
        using (var scope2 = provider.CreateScope())
        {
            instance2 = scope2.ServiceProvider.GetService<ICreateOrganizationIntegrationConfigurationCommand>();
        }
        using (var scope3 = provider.CreateScope())
        {
            instance3 = scope3.ServiceProvider.GetService<ICreateOrganizationIntegrationConfigurationCommand>();
        }

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotNull(instance3);
        Assert.NotSame(instance1, instance2);
        Assert.NotSame(instance2, instance3);
        Assert.NotSame(instance1, instance3);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_CommandsQueries__SameInstanceWithinScope()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        var provider = _services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var instance1 = scope.ServiceProvider.GetService<ICreateOrganizationIntegrationConfigurationCommand>();
        var instance2 = scope.ServiceProvider.GetService<ICreateOrganizationIntegrationConfigurationCommand>();

        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddEventIntegrationsCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);
        _services.AddEventIntegrationsCommandsQueries(_globalSettings);

        var createConfigCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand)).ToList();
        Assert.Single(createConfigCmdDescriptors);

        var updateIntegrationCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(IUpdateOrganizationIntegrationCommand)).ToList();
        Assert.Single(updateIntegrationCmdDescriptors);
    }

    [Fact]
    public void AddOrganizationIntegrationCommandsQueries_RegistersAllIntegrationServices()
    {
        _services.AddOrganizationIntegrationCommandsQueries();

        Assert.Contains(_services, s => s.ServiceType == typeof(ICreateOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IUpdateOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IDeleteOrganizationIntegrationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IGetOrganizationIntegrationsQuery));
    }

    [Fact]
    public void AddOrganizationIntegrationCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddOrganizationIntegrationCommandsQueries();
        _services.AddOrganizationIntegrationCommandsQueries();
        _services.AddOrganizationIntegrationCommandsQueries();

        var createCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationCommand)).ToList();
        Assert.Single(createCmdDescriptors);
    }

    [Fact]
    public void AddOrganizationIntegrationConfigurationCommandsQueries_RegistersAllConfigurationServices()
    {
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();

        Assert.Contains(_services, s => s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IUpdateOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IDeleteOrganizationIntegrationConfigurationCommand));
        Assert.Contains(_services, s => s.ServiceType == typeof(IGetOrganizationIntegrationConfigurationsQuery));
    }

    [Fact]
    public void AddOrganizationIntegrationConfigurationCommandsQueries_MultipleCalls_IsIdempotent()
    {
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();
        _services.AddOrganizationIntegrationConfigurationCommandsQueries();

        var createCmdDescriptors = _services.Where(s =>
            s.ServiceType == typeof(ICreateOrganizationIntegrationConfigurationCommand)).ToList();
        Assert.Single(createCmdDescriptors);
    }

    private static GlobalSettings CreateGlobalSettings(Dictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var settings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(settings);
        return settings;
    }
}
