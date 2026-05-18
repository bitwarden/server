using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Enums;
using Bit.Core.Platform.Installations;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Platform;

public class RelayPushRegistrationServiceTests
{
    private readonly FakeLogCollector _logCollector;
    private readonly RelayPushRegistrationService _sut;

    public RelayPushRegistrationServiceTests()
    {
        var cloudApi = new ApiApplicationFactory();
        cloudApi.SubstituteService<IPushRegistrationService>(service => { });

        var fakeInstallationId = Guid.NewGuid();

        cloudApi.Identity.SubstituteService<IInstallationRepository>(service =>
        {
            service.GetByIdAsync(fakeInstallationId)
                .Returns(new Installation
                {
                    Id = fakeInstallationId,
                    Key = "test_key",
                    Enabled = true,
                });
        });

        var cloudApiHttpClient = cloudApi.CreateClient();
        var cloudIdentityHttpClient = cloudApi.Identity.CreateClient();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();

        httpClientFactory.CreateClient("client")
            .Returns(cloudApiHttpClient);

        httpClientFactory.CreateClient("identity")
            .Returns(cloudIdentityHttpClient);

        var globalSettings = new GlobalSettings
        {
            PushRelayBaseUri = "http://api.localhost"
        };
        globalSettings.Installation.IdentityUri = "http://identity.localhost";
        globalSettings.Installation.Id = fakeInstallationId;
        globalSettings.Installation.Key = "test_key";

        var logger = new FakeLogger<RelayPushRegistrationService>();

        _logCollector = logger.Collector;

        _sut = new RelayPushRegistrationService(
            httpClientFactory,
            globalSettings,
            logger
        );
    }

    [Fact]
    public async Task BrowserExtensionData_ShouldNotLogIssues()
    {
        await _sut.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData("endpoint", "p256dh", "auth"),
            deviceId: Guid.NewGuid().ToString(),
            userId: Guid.NewGuid().ToString(),
            identifier: Guid.NewGuid().ToString(),
            DeviceType.ChromeExtension,
            organizationIds: [Guid.NewGuid().ToString()],
            installationId: Guid.NewGuid()
        );

        var logs = _logCollector.GetSnapshot();

        Assert.DoesNotContain(logs, l => l.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task MobileData_ShouldNotLogIssues()
    {
        await _sut.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData("PushToken"),
            deviceId: Guid.NewGuid().ToString(),
            userId: Guid.NewGuid().ToString(),
            identifier: Guid.NewGuid().ToString(),
            DeviceType.ChromeExtension,
            organizationIds: [Guid.NewGuid().ToString()],
            installationId: Guid.NewGuid()
        );

        var logs = _logCollector.GetSnapshot();

        Assert.DoesNotContain(logs, l => l.Level >= LogLevel.Warning);
    }
}
