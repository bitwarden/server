using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class InMemoryApplicationCacheServiceTests
{
    private readonly InMemoryApplicationCacheService _sut;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IVNextInMemoryApplicationCacheService _nNextInMemoryApplicationCacheService;

    public InMemoryApplicationCacheServiceTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _nNextInMemoryApplicationCacheService = Substitute.For<IVNextInMemoryApplicationCacheService>();

        _sut = new InMemoryApplicationCacheService(_organizationRepository, _providerRepository, _nNextInMemoryApplicationCacheService, useVNext: false);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}

public class InMemoryApplicationCacheServiceWithVNextTests
{
    private readonly InMemoryApplicationCacheService _sut;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IVNextInMemoryApplicationCacheService _vNextInMemoryApplicationCacheService;

    public InMemoryApplicationCacheServiceWithVNextTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _vNextInMemoryApplicationCacheService = Substitute.For<IVNextInMemoryApplicationCacheService>();

        _sut = new InMemoryApplicationCacheService(_organizationRepository, _providerRepository, _vNextInMemoryApplicationCacheService, useVNext: true);
    }

    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
