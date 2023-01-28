using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class NotificationHubPushRegistrationServiceTests
{
    private readonly NotificationHubPushRegistrationService _sut;

    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly GlobalSettings _globalSettings;

    public NotificationHubPushRegistrationServiceTests()
    {
        _installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();
        _globalSettings = new GlobalSettings();

        _sut = new NotificationHubPushRegistrationService(
            _installationDeviceRepository,
            _globalSettings
        );
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
