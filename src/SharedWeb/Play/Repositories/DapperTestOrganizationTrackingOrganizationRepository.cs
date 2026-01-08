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
    private readonly IPlayDataService _playDataService;

    public DapperTestOrganizationTrackingOrganizationRepository(
        IPlayDataService playDataService,
        GlobalSettings globalSettings,
        ILogger<OrganizationRepository> logger)
        : base(globalSettings, logger)
    {
        _playDataService = playDataService;
    }

    public override async Task<Organization> CreateAsync(Organization obj)
    {
        var createdOrganization = await base.CreateAsync(obj);
        await _playDataService.Record(createdOrganization);
        return createdOrganization;
    }
}
