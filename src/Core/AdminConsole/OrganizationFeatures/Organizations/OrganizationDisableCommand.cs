using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDisableCommand : IOrganizationDisableCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;

    public OrganizationDisableCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        _organizationRepository = organizationRepository;
        _organizationAbilityCacheService = organizationAbilityCacheService;
    }

    public async Task DisableAsync(Guid organizationId, DateTime? expirationDate)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is { Enabled: true })
        {
            organization.Enabled = false;
            organization.ExpirationDate = expirationDate;
            organization.RevisionDate = DateTime.UtcNow;

            await _organizationRepository.ReplaceAsync(organization);
            await _organizationAbilityCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }
}
