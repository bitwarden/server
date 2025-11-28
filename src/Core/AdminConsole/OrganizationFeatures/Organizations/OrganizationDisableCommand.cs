using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationDisableCommand : IOrganizationDisableCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationCacheService _applicationCacheService;

    public OrganizationDisableCommand(
        IOrganizationRepository organizationRepository,
        IApplicationCacheService applicationCacheService)
    {
        _organizationRepository = organizationRepository;
        _applicationCacheService = applicationCacheService;
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
            await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }
}
