using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// See <see cref="IPreAccessPolicyEnforcer"/>.
/// </summary>
/// <param name="policies">
/// The target organization's policies. Disabled policies are ignored. Leave empty if the organization cannot use
/// policies.
/// </param>
/// <param name="providerUserIds">The ids of users who are provider users for the target organization.</param>
/// <param name="factories">
/// The registered policy requirement factories. Only those implementing <see cref="IPreAccessPolicyRequirementFactory"/>
/// are supported.
/// </param>
public class PreAccessPolicyEnforcer(
    IEnumerable<Policy> policies,
    IEnumerable<Guid> providerUserIds,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IPreAccessPolicyEnforcer
{
    private readonly Dictionary<PolicyType, Policy> _enabledPolicies = policies
        .Where(p => p.Enabled)
        .ToDictionary(p => p.Type);

    private readonly HashSet<Guid> _providerUserIds = providerUserIds.ToHashSet();

    public PreAccessPolicyDecision Evaluate(PolicyType policyType, Guid userId, OrganizationUserType proposedRole)
    {
        var factory = factories
                          .OfType<IPreAccessPolicyRequirementFactory>()
                          .SingleOrDefault(f => f.PolicyType == policyType)
            ?? throw new NotImplementedException("No Requirement Factory found for " + policyType);

        if (!_enabledPolicies.TryGetValue(policyType, out var policy))
        {
            return PreAccessPolicyDecision.NotEnforced;
        }

        return factory.EnforcePreAccess(proposedRole, _providerUserIds.Contains(userId))
            ? PreAccessPolicyDecision.Enforced(policy.Data)
            : PreAccessPolicyDecision.NotEnforced;
    }
}
