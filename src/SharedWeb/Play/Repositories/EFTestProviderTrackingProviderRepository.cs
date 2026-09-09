using AutoMapper;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// EntityFramework decorator around the <see cref="Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.ProviderRepository"/> that tracks
/// created Providers for seeding.
/// </summary>
public class EFTestProviderTrackingProviderRepository : ProviderRepository
{
    private readonly IPlayItemService _playItemService;

    public EFTestProviderTrackingProviderRepository(
        IPlayItemService playItemService,
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper)
        : base(serviceScopeFactory, mapper)
    {
        _playItemService = playItemService;
    }

    public override async Task<Provider> CreateAsync(Provider obj)
    {
        var createdProvider = await base.CreateAsync(obj);
        await _playItemService.Record(createdProvider);
        return createdProvider;
    }
}
