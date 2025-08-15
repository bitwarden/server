using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.Implementations;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class InMemoryApplicationCacheServiceTests
{
    private readonly InMemoryApplicationCacheService _sut;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IVNextInMemoryApplicationCacheService _nNextInMemoryApplicationCacheService;
    private readonly IFeatureService _featureService;

    public InMemoryApplicationCacheServiceTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _nNextInMemoryApplicationCacheService = Substitute.For<IVNextInMemoryApplicationCacheService>();
        _featureService = Substitute.For<IFeatureService>();
        _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache).Returns(false);

        _sut = new InMemoryApplicationCacheService(_organizationRepository, _providerRepository, _nNextInMemoryApplicationCacheService, _featureService);
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
    private readonly IFeatureService _featureService;

    public InMemoryApplicationCacheServiceWithVNextTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _providerRepository = Substitute.For<IProviderRepository>();
        var vNextInMemoryApplicationCacheService = new VNextInMemoryApplicationCacheService(_organizationRepository, _providerRepository, new FakeTimeProvider());
        _featureService = Substitute.For<IFeatureService>();
        _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache).Returns(true);

        _sut = new InMemoryApplicationCacheService(_organizationRepository, _providerRepository, vNextInMemoryApplicationCacheService, _featureService);
    }

    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
