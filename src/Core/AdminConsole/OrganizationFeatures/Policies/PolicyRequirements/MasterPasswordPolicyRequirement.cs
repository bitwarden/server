using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send policy.
/// </summary>
public class MasterPasswordPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the user. If true, the user should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool MasterPassword { get; init; }
}

public class MasterPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<MasterPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.MasterPassword;

    public override MasterPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new MasterPasswordPolicyRequirement { MasterPassword = policyDetails.Any() };
        return result;
    }
}
