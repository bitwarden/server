using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class InMemoryServiceBusApplicationCacheServiceTests
{
    private readonly InMemoryServiceBusApplicationCacheService _sut;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly GlobalSettings _globalSettings;

    public InMemoryServiceBusApplicationCacheServiceTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _globalSettings = new GlobalSettings();

        _sut = new InMemoryServiceBusApplicationCacheService(
            _organizationRepository,
            _providerRepository,
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
