using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Repositories;
using Bit.Core.Repositories.Noop;
using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.Core.Test.Platform.PushRegistration;

public class PushRegistrationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPushRegistration_Cloud_CreatesNotificationHubRegistrationService()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "false" },
        });

        var pushRegistrationService = services.GetRequiredService<IPushRegistrationService>();
        Assert.IsType<NotificationHubPushRegistrationService>(pushRegistrationService);
    }

    [Fact]
    public void AddPushRegistration_SelfHosted_NoOtherConfig_ReturnsNoopRegistrationService()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
        });

        var pushRegistrationService = services.GetRequiredService<IPushRegistrationService>();
        Assert.IsType<NoopPushRegistrationService>(pushRegistrationService);
    }

    [Fact]
    public void AddPushRegistration_SelfHosted_RelayConfig_ReturnsRelayRegistrationService()
    {
        var services = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:PushRelayBaseUri", "https://example.com" },
            { "GlobalSettings:Installation:Key", "some_key" },
        });

        var pushRegistrationService = services.GetRequiredService<IPushRegistrationService>();
        Assert.IsType<RelayPushRegistrationService>(pushRegistrationService);
    }

    [Fact]
    public void AddPushRegistration_MultipleTimes_NoAdditionalServices()
    {
        var services = new ServiceCollection();

        var config = new Dictionary<string, string?>
        {
            { "GlobalSettings:SelfHosted", "true" },
            { "GlobalSettings:PushRelayBaseUri", "https://example.com" },
            { "GlobalSettings:Installation:Key", "some_key" },
        };

        AddServices(services, config);

        // Add services again
        services.AddPushRegistration();

        var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IPushRegistrationService>());
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


        // Temporary until AddPushRegistration can add it themselves directly.
        services.TryAddSingleton<IInstallationDeviceRepository, InstallationDeviceRepository>();

        services.AddPushRegistration();
    }
}
