using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Send Controls policy.
/// </summary>
public class SendControlsPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the user. If true, the user should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool DisableSend { get; init; }

    /// <summary>
    /// Indicates whether the user is prohibited from hiding their email from the recipient of a Send.
    /// </summary>
    public bool DisableHideEmail { get; init; }
}

public class SendControlsPolicyRequirementFactory : BasePolicyRequirementFactory<SendControlsPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.SendControls;

    public override SendControlsPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        return policyDetails
            .Select(p => p.GetDataModel<SendControlsPolicyData>())
            .Aggregate(
                new SendControlsPolicyRequirement(),
                (result, data) => new SendControlsPolicyRequirement
                {
                    DisableSend = result.DisableSend || data.DisableSend,
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail,
                });
    }
}
