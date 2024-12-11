using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class AzureQueueEventWriteServiceTests
{
    private readonly AzureQueueEventWriteService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly IEventRepository _eventRepository;

    public AzureQueueEventWriteServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _eventRepository = Substitute.For<IEventRepository>();

        _sut = new AzureQueueEventWriteService(_globalSettings);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
