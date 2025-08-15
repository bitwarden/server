using Bit.Core.AdminConsole.Repositories;
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
    private readonly IVNextInMemoryApplicationCacheService _ivNextInMemoryApplicationCacheService;
    private readonly IFeatureService _featureService;

    public InMemoryServiceBusApplicationCacheServiceTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _ivNextInMemoryApplicationCacheService = Substitute.For<IVNextInMemoryApplicationCacheService>();
        _featureService = Substitute.For<IFeatureService>();
        _globalSettings = new GlobalSettings();

        _sut = new InMemoryServiceBusApplicationCacheService(
            _organizationRepository,
            _providerRepository,
            _globalSettings,
            _ivNextInMemoryApplicationCacheService,
            _featureService);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
