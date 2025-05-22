#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Two-Factor Authentication policy at the organization level.
/// </summary>
public class OrganizationTwoFactorPolicyRequirement : IPolicyRequirement
{
    private readonly Policy? _policy;

    public OrganizationTwoFactorPolicyRequirement(Policy? policy)
    {
        _policy = policy;
    }

    /// <summary>
    /// Determines if the organization requires two-factor authentication.
    /// </summary>
    public bool IsRequired => _policy != null && _policy.Enabled;
}

/// <summary>
/// Organization policy requirement factory for the Require Two-Factor Authentication policy.
/// </summary>
public class OrganizationTwoFactorPolicyRequirementFactory
    : IOrganizationPolicyRequirementFactory<OrganizationTwoFactorPolicyRequirement>
{
    public PolicyType PolicyType => PolicyType.TwoFactorAuthentication;

    public OrganizationTwoFactorPolicyRequirement Create(Policy? policy)
    {
        return new OrganizationTwoFactorPolicyRequirement(policy);
    }
}
