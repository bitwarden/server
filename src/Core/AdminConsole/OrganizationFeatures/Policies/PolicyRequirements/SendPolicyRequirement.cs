using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send and Send Options policies.
/// </summary>
public class SendPolicyRequirement : IPolicyRequirement
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

public class SendPolicyRequirementFactory : SimpleRequirementFactory<SendPolicyRequirement>
{
    protected override IEnumerable<OrganizationUserType> ExemptRoles =>
        [OrganizationUserType.Owner, OrganizationUserType.Admin];

    public override IEnumerable<PolicyType> PolicyTypes => [PolicyType.SendOptions, PolicyType.DisableSend];

    public override SendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        policyDetails = policyDetails.ToList();
        var result = policyDetails
            .GetPolicyType(PolicyType.SendOptions)
            .Select(p => p.GetDataModel<SendOptionsPolicyData>())
            .Aggregate(
                new SendPolicyRequirement
                {
                    // Set Disable Send requirement in the initial seed
                    DisableSend = policyDetails.GetPolicyType(PolicyType.DisableSend).Any()
                },
                (result, data) => new SendPolicyRequirement
                {
                    DisableSend = result.DisableSend,
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });

        return result;
    }
}
