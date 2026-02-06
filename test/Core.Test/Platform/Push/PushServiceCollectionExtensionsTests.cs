using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Bit.Core.Repositories.Noop;
using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.Core.Test.Platform.Push;

public class PushServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPush_SelfHosted_NoConfig_NoEngines()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        Assert.Empty(engines);
    }

    [Fact]
    public void AddPush_SelfHosted_ConfiguredForRelay_RelayEngineAdded()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
            { "GlobalSettings:Installation:Key", "some_key"},
            { "GlobalSettings:PushRelayBaseUri", "https://example.com" },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        var engine = Assert.Single(engines);
        Assert.IsType<RelayPushEngine>(engine);
    }

    [Fact]
    public void AddPush_SelfHosted_ConfiguredForApi_ApiEngineAdded()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
            { "GlobalSettings:InternalIdentityKey", "some_key"},
            { "GlobalSettings:BaseServiceUri", "https://example.com" },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        var engine = Assert.Single(engines);
        Assert.IsType<NotificationsApiPushEngine>(engine);
    }

    [Fact]
    public void AddPush_SelfHosted_ConfiguredForRelayAndApi_TwoEnginesAdded()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:Installation:Id", Guid.NewGuid().ToString() },
            { "GlobalSettings:Installation:Key", "some_key"},
            { "GlobalSettings:PushRelayBaseUri", "https://example.com" },
            { "GlobalSettings:InternalIdentityKey", "some_key"},
            { "GlobalSettings:BaseServiceUri", "https://example.com" },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        Assert.Collection(
            engines,
            e => Assert.IsType<RelayPushEngine>(e),
            e => Assert.IsType<NotificationsApiPushEngine>(e)
        );
    }

    [Fact]
    public void AddPush_Cloud_NoConfig_AddsNotificationHub()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "false" },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        var engine = Assert.Single(engines);
        Assert.IsType<NotificationHubPushEngine>(engine);
    }

    [Fact]
    public void AddPush_Cloud_HasNotificationConnectionString_TwoEngines()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "false" },
            { "GlobalSettings:Notifications:ConnectionString", "UseDevelopmentStorage=true" },
        });

        _ = services.GetRequiredService<IPushNotificationService>();
        var engines = services.GetServices<IPushEngine>();

        Assert.Collection(
            engines,
            e => Assert.IsType<NotificationHubPushEngine>(e),
            e => Assert.IsType<AzureQueuePushEngine>(e)
        );
    }

    [Fact]
    public void AddPush_Cloud_CalledTwice_DoesNotAddServicesTwice()
    {
        var services = new ServiceCollection();

        var config = new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "false" },
            { "GlobalSettings:Notifications:ConnectionString", "UseDevelopmentStorage=true" },
        };

        AddServices(services, config);

        var initialCount = services.Count;

        // Add services again
        AddServices(services, config);

        Assert.Equal(initialCount, services.Count);
    }

    private static ServiceProvider Build(Dictionary<string, string?> initialData)
    {
        var services = new ServiceCollection();

        AddServices(services, initialData);

        return services.BuildServiceProvider();
    }

    private static void AddServices(IServiceCollection services, Dictionary<string, string?> initialData)
    {
        // A minimal service collection is always expected to have logging, config, and global settings
        // pre-registered. 

        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        services.TryAddSingleton(config);
        var globalSettings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(globalSettings);

        services.TryAddSingleton(globalSettings);
        services.TryAddSingleton<IGlobalSettings>(globalSettings);

        // Temporary until AddPush can add it themselves directly.
        services.TryAddSingleton<IDeviceRepository, StubDeviceRepository>();

        // Temporary until AddPush can add it themselves directly.
        services.TryAddSingleton<IInstallationDeviceRepository, InstallationDeviceRepository>();

        services.AddPush(globalSettings);
    }

    private class StubDeviceRepository : IDeviceRepository
    {
        public Task ClearPushTokenAsync(Guid id) => throw new NotImplementedException();
        public Task<Device> CreateAsync(Device obj) => throw new NotImplementedException();
        public Task DeleteAsync(Device obj) => throw new NotImplementedException();
        public Task<Device?> GetByIdAsync(Guid id, Guid userId) => throw new NotImplementedException();
        public Task<Device?> GetByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<Device?> GetByIdentifierAsync(string identifier) => throw new NotImplementedException();
        public Task<Device?> GetByIdentifierAsync(string identifier, Guid userId) => throw new NotImplementedException();
        public Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId) => throw new NotImplementedException();
        public Task<ICollection<DeviceAuthDetails>> GetManyByUserIdWithDeviceAuth(Guid userId) => throw new NotImplementedException();
        public Task ReplaceAsync(Device obj) => throw new NotImplementedException();
        public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<Device> devices) => throw new NotImplementedException();
        public Task UpsertAsync(Device obj) => throw new NotImplementedException();
    }
}
