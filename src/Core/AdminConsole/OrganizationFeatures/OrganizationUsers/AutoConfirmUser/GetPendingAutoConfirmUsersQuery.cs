using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class GetPendingAutoConfirmUsersQuery(
    IApplicationCacheService applicationCacheService,
    IPolicyQuery policyQuery,
    IOrganizationUserRepository organizationUserRepository) : IGetPendingAutoConfirmUsersQuery
{
    public async Task<ICollection<OrganizationUser>> GetPendingAutoConfirmUsersAsync(Guid organizationId)
    {
        var orgAbility = await applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        if (orgAbility is not { UseAutomaticUserConfirmation: true })
        {
            return [];
        }

        var autoConfirmPolicy = await policyQuery.RunAsync(organizationId, PolicyType.AutomaticUserConfirmation);
        if (!autoConfirmPolicy.Enabled)
        {
            return [];
        }

        return await organizationUserRepository.GetManyPendingAutoConfirmAsync(organizationId);
    }
}
