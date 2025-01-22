using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationEnableCommand : IOrganizationEnableCommand
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationEnableCommand(
        IApplicationCacheService applicationCacheService,
        IOrganizationRepository organizationRepository)
    {
        _applicationCacheService = applicationCacheService;
        _organizationRepository = organizationRepository;
    }

    public async Task EnableAsync(Guid organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is { Enabled: false })
        {
            organization.Enabled = true;

            await _organizationRepository.ReplaceAsync(organization);
            await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    public async Task EnableAsync(Guid organizationId, DateTime? expirationDate)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is { Enabled: false, Gateway: not null })
        {
            organization.Enabled = true;
            organization.ExpirationDate = expirationDate;
            organization.RevisionDate = DateTime.UtcNow;

            await _organizationRepository.ReplaceAsync(organization);
            await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }
}
