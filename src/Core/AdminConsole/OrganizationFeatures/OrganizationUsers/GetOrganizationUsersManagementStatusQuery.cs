using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class GetOrganizationUsersManagementStatusQuery : IGetOrganizationUsersManagementStatusQuery
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetOrganizationUsersManagementStatusQuery(
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<IDictionary<Guid, bool>> GetUsersOrganizationManagementStatusAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds)
    {
        if (organizationUserIds.Any())
        {
            // Users can only be managed by an Organization that is enabled and can have organization domains
            var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);

            // TODO: Replace "UseSso" with a new organization ability like "UseOrganizationDomains" (PM-11622).
            // Verified domains were tied to SSO, so we currently check the "UseSso" organization ability.
            if (organizationAbility is { Enabled: true, UseSso: true })
            {
                // Get all organization users with claimed domains by the organization
                var organizationUsersWithClaimedDomain = await _organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organizationId);

                // Create a dictionary with the OrganizationUserId and a boolean indicating if the user is managed by the organization
                return organizationUserIds.ToDictionary(ouId => ouId, ouId => organizationUsersWithClaimedDomain.Any(ou => ou.Id == ouId));
            }
        }

        return organizationUserIds.ToDictionary(ouId => ouId, _ => false);
    }
}
