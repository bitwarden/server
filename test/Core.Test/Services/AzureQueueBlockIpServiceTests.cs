using Bit.Core.Services;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.Services;

public class AzureQueueBlockIpServiceTests
{
    private readonly AzureQueueBlockIpService _sut;

    private readonly GlobalSettings _globalSettings;

    public AzureQueueBlockIpServiceTests()
    {
        _globalSettings = new GlobalSettings();

        _sut = new AzureQueueBlockIpService(_globalSettings);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
