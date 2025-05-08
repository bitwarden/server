using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;

/// <summary>
/// Policy requirements for the Require Two-Factor Authentication policy.
/// </summary>
public class RequireTwoFactorPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether two-factor authentication is required for the user.
    /// </summary>
    public bool RequireTwoFactor { get; init; }
}

public class RequireTwoFactorPolicyRequirementFactory : BasePolicyRequirementFactory<RequireTwoFactorPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.TwoFactorAuthentication;
    protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => [OrganizationUserStatusType.Revoked];

    public override RequireTwoFactorPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        return new RequireTwoFactorPolicyRequirement
        {
            RequireTwoFactor = policyDetails.Any(p => p.PolicyType == PolicyType.TwoFactorAuthentication)
        };
    }
}
