using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class GetOrganizationUsersManagementStatusQuery : IGetOrganizationUsersManagementStatusQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetOrganizationUsersManagementStatusQuery(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<IDictionary<Guid, bool>> GetUsersOrganizationManagementStatusAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds)
    {
        if (organizationUserIds.Any())
        {
            // Users can only be managed by an enabled Organization that is on an Enterprise plan
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization is { Enabled: true })
            {
                var plan = StaticStore.GetPlan(organization.PlanType);
                if (plan.ProductTier == ProductTierType.Enterprise)
                {
                    // Get all organization users with claimed domains by the organization
                    var organizationUsersWithClaimedDomain = await _organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organizationId);

                    // Create a dictionary with the OrganizationUserId and a boolean indicating if the user is managed by the organization
                    return organizationUserIds.ToDictionary(ouId => ouId, ouId => organizationUsersWithClaimedDomain.Any(ou => ou.Id == ouId));
                }
            }
        }

        return organizationUserIds.ToDictionary(ouId => ouId, _ => false);
    }
}
