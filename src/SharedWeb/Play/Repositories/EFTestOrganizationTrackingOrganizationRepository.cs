using AutoMapper;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.SharedWeb.Play.Repositories;

/// <summary>
/// EntityFramework decorator around the <see cref="Bit.Infrastructure.EntityFramework.Repositories.OrganizationRepository"/> that tracks
/// created Organizations for seeding.
/// </summary>
public class EFTestOrganizationTrackingOrganizationRepository : OrganizationRepository
{
    private readonly IPlayDataService _playDataService;

    public EFTestOrganizationTrackingOrganizationRepository(
        IPlayDataService playDataService,
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper,
        ILogger<OrganizationRepository> logger)
        : base(serviceScopeFactory, mapper, logger)
    {
        _playDataService = playDataService;
    }

    public override async Task<Core.AdminConsole.Entities.Organization> CreateAsync(Core.AdminConsole.Entities.Organization organization)
    {
        var createdOrganization = await base.CreateAsync(organization);
        await _playDataService.Record(createdOrganization);
        return createdOrganization;
    }
}
