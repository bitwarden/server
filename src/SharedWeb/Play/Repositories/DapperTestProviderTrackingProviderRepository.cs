using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.AdminConsole.Repositories;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// Dapper decorator around the <see cref="Bit.Infrastructure.Dapper.AdminConsole.Repositories.ProviderRepository"/> that tracks
/// created Providers for seeding.
/// </summary>
public class DapperTestProviderTrackingProviderRepository : ProviderRepository
{
    private readonly IPlayItemService _playItemService;

    public DapperTestProviderTrackingProviderRepository(
        IPlayItemService playItemService,
        GlobalSettings globalSettings)
        : base(globalSettings)
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
