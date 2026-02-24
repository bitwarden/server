using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Send policy.
/// </summary>
public class SendPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the org. If true, the org users should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool DisableSend { get; init; }

    /// <summary>
    /// Indicates whether the org users are prohibited from hiding their email from the recipient of a Send.
    /// </summary>
    public bool DisableHideEmail { get; init; }
}

public class SendPolicyRequirementFactory : BasePolicyRequirementFactory<SendPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SendOptions;

    public override SendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        return policyDetails
            .Select(p => p.GetDataModel<SendPolicyData>())
            .Aggregate(
                new SendPolicyRequirement(),
                (result, data) => new SendPolicyRequirement
                {
                    DisableSend = result.DisableSend || data.DisableSend,
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });
    }
}
