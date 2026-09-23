using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
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

        var emptyPolicies = new Dictionary<PolicyType, Policy>();
        if (!organizationAbility.Enabled || !organizationAbility.UsePolicies)
        {
            return new PreAccessPolicyEnforcer(organizationId, emptyPolicies, [], factories);
        }

        var enabledPolicies = (await policyRepository.GetManyByOrganizationIdAsync(organizationId))
            .Where(p => p.Enabled)
            .ToDictionary(p => p.Type);
        if (enabledPolicies.Count == 0)
        {
            return new PreAccessPolicyEnforcer(organizationId, emptyPolicies, [], factories);
        }

        // Matches the IsProvider calculation used by general policy enforcement: any provider user of a provider
        // that manages the organization, regardless of status.
        var providerUserIds = (await providerUserRepository.GetManyByOrganizationAsync(organizationId))
            .Where(providerUser => providerUser.UserId.HasValue)
            .Select(providerUser => providerUser.UserId!.Value);

        return new PreAccessPolicyEnforcer(organizationId, enabledPolicies, providerUserIds, factories);
    }
}
