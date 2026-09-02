using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send policy.
/// </summary>
public class DisableSendPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the user. If true, the user should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool DisableSend { get; init; }
}

public class DisableSendPolicyRequirementFactory : BasePolicyRequirementFactory<DisableSendPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.DisableSend;

    public override DisableSendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var result = new DisableSendPolicyRequirement { DisableSend = policyDetails.Any() };
        return result;
    }
}
