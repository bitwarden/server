using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// See <see cref="IPreAccessEnforcerQuery"/>.
/// </summary>
public class PreAccessEnforcerQuery(
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IPolicyRepository policyRepository,
    IProviderUserRepository providerUserRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IPreAccessEnforcerQuery
{
    public async Task<IPreAccessPolicyEnforcer> RunAsync(Guid organizationId)
    {
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId)
            ?? throw new PreAccessOrganizationNotFoundException();

        if (!organizationAbility.Enabled || !organizationAbility.UsePolicies)
        {
            return new PreAccessPolicyEnforcer([], [], factories);
        }

        var policies = await policyRepository.GetManyByOrganizationIdAsync(organizationId);

        // Accepted and Confirmed provider users are always linked to a user account; Invited users are not.
        var providerUserIds = (await providerUserRepository.GetManyByOrganizationAsync(organizationId))
            .Where(providerUser => providerUser.Status is ProviderUserStatusType.Accepted or ProviderUserStatusType.Confirmed)
            .Select(providerUser => providerUser.UserId!.Value);

        return new PreAccessPolicyEnforcer(policies, providerUserIds, factories);
    }
}
