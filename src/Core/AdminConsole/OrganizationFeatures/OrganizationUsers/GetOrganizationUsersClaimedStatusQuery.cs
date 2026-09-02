using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class GetOrganizationUsersClaimedStatusQuery : IGetOrganizationUsersClaimedStatusQuery
{
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetOrganizationUsersClaimedStatusQuery(
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<IDictionary<Guid, bool>> GetUsersOrganizationClaimedStatusAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds)
    {
        if (organizationUserIds.Any())
        {
            // Users can only be claimed by an Organization that is enabled and can have organization domains
            var organizationAbility = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);

            if (organizationAbility is { Enabled: true, UseOrganizationDomains: true })
            {
                var organizationUsersWithClaimedDomain = await _organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organizationId);

                var claimedIds = organizationUsersWithClaimedDomain.Select(ou => ou.Id).ToHashSet();
                return organizationUserIds.ToDictionary(ouId => ouId, claimedIds.Contains);
            }
        }

        return organizationUserIds.ToDictionary(ouId => ouId, _ => false);
    }
}
