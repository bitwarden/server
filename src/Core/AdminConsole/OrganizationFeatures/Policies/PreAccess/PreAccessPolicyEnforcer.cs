using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// See <see cref="IPreAccessPolicyEnforcer"/>.
/// </summary>
/// <param name="enabledPolicies">
/// The target organization's enabled policies, keyed by type. Leave empty if the organization cannot use policies.
/// </param>
/// <param name="providerUserIds">The ids of users who are provider users for the target organization.</param>
/// <param name="factories">
/// The registered policy requirement factories. Only those implementing <see cref="IPreAccessPolicyRequirementFactory"/>
/// are supported.
/// </param>
public class PreAccessPolicyEnforcer(
    IReadOnlyDictionary<PolicyType, Policy> enabledPolicies,
    IEnumerable<Guid> providerUserIds,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IPreAccessPolicyEnforcer
{
    private readonly HashSet<Guid> _providerUserIds = providerUserIds.ToHashSet();

    public PreAccessPolicyResult Evaluate(PolicyType policyType, Guid userId, OrganizationUserType proposedRole)
    {
        var factory = factories
                          .OfType<IPreAccessPolicyRequirementFactory>()
                          .SingleOrDefault(f => f.PolicyType == policyType)
            ?? throw new NotImplementedException("No Requirement Factory found for " + policyType);

        if (!enabledPolicies.TryGetValue(policyType, out var policy))
        {
            return PreAccessPolicyResult.NotEnforced;
        }

        return factory.EnforcePreAccess(proposedRole, _providerUserIds.Contains(userId))
            ? new PreAccessPolicyResult(true, policy.Data)
            : PreAccessPolicyResult.NotEnforced;
    }
}
