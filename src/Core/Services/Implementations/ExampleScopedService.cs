namespace Bit.Core.Services.Implementations;

public class ExampleScopedService : IExampleScopedService
{
    private readonly IFeatureService _featureService;
    private readonly IVNextInMemoryApplicationCacheService _vNextInMemoryApplicationCacheService;
    private readonly InMemoryApplicationCacheService _inMemoryApplicationCacheService;

    public ExampleScopedService(
        IFeatureService featureService,
        IVNextInMemoryApplicationCacheService vNextInMemoryApplicationCacheService,
        InMemoryApplicationCacheService inMemoryApplicationCacheService)
    {
        _featureService = featureService;
        _vNextInMemoryApplicationCacheService = vNextInMemoryApplicationCacheService;
        _inMemoryApplicationCacheService = inMemoryApplicationCacheService;
    }
}
