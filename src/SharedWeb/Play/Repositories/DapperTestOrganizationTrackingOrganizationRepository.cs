using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// Dapper decorator around the <see cref="Bit.Infrastructure.Dapper.Repositories.OrganizationRepository"/> that tracks
/// created Organizations for seeding.
/// </summary>
public class DapperTestOrganizationTrackingOrganizationRepository : OrganizationRepository
{
    private readonly IPlayItemService _playItemService;

    public DapperTestOrganizationTrackingOrganizationRepository(
        IPlayItemService playItemService,
        GlobalSettings globalSettings,
        ILogger<OrganizationRepository> logger)
        : base(globalSettings, logger)
    {
        _playItemService = playItemService;
    }

    public override async Task<Organization> CreateAsync(Organization obj)
    {
        var createdOrganization = await base.CreateAsync(obj);
        await _playItemService.Record(createdOrganization);
        return createdOrganization;
    }
}
