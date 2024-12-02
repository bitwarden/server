using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Platform.Push.Test;

public class AzureQueuePushNotificationServiceTests
{
    private readonly AzureQueuePushNotificationService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AzureQueuePushNotificationServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        _sut = new AzureQueuePushNotificationService(
            _globalSettings,
            _httpContextAccessor
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
