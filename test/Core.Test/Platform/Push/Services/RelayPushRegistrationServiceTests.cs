using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Platform.Push.Test;

public class RelayPushRegistrationServiceTests
{
    private readonly RelayPushRegistrationService _sut;

    private readonly IHttpClientFactory _httpFactory;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<RelayPushRegistrationService> _logger;

    public RelayPushRegistrationServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _httpFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<RelayPushRegistrationService>>();

        _sut = new RelayPushRegistrationService(
            _httpFactory,
            _globalSettings,
            _logger
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
