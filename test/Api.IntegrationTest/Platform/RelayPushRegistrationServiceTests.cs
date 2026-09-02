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
    private readonly ApiApplicationFactory _cloudApi;
    private readonly Guid _fakeInstallationId;
    private readonly FakeLogCollector _logCollector;
    private readonly RelayPushRegistrationService _sut;

    public RelayPushRegistrationServiceTests()
    {
        _cloudApi = new ApiApplicationFactory();
        _cloudApi.SubstituteService<IPushRegistrationService>(service => { });

        _fakeInstallationId = Guid.NewGuid();

        _cloudApi.Identity.SubstituteService<IInstallationRepository>(service =>
        {
            service.GetByIdAsync(_fakeInstallationId)
                .Returns(new Installation
                {
                    Id = _fakeInstallationId,
                    Key = "test_key",
                    Enabled = true,
                });
        });

        var cloudApiHttpClient = _cloudApi.CreateClient();
        var cloudIdentityHttpClient = _cloudApi.Identity.CreateClient();

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
        globalSettings.Installation.Id = _fakeInstallationId;
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
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identifier = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();

        await _sut.CreateOrUpdateRegistrationAsync(
            new PushRegistrationData("PushToken"),
            deviceId.ToString(),
            userId.ToString(),
            identifier.ToString(),
            DeviceType.iOS,
            [organizationId.ToString()],
            installationId
        );

        var logs = _logCollector.GetSnapshot();

        Assert.DoesNotContain(logs, l => l.Level >= LogLevel.Warning);

        // Mobile should also actually successfully make it to the cloud push registration service
        // with all of its data prefixed with the self host installation id.
        var mockPushRegistrationService = _cloudApi.GetService<IPushRegistrationService>();
        await mockPushRegistrationService
            .Received(1)
            .CreateOrUpdateRegistrationAsync(
                new PushRegistrationData("PushToken"),
                deviceId: $"{_fakeInstallationId}_{deviceId}",
                userId: $"{_fakeInstallationId}_{userId}",
                identifier: $"{_fakeInstallationId}_{identifier}",
                type: DeviceType.iOS,
                Arg.Is<IEnumerable<string>>(v => v.Single() == $"{_fakeInstallationId}_{organizationId}"),
                installationId
            );
    }
}
