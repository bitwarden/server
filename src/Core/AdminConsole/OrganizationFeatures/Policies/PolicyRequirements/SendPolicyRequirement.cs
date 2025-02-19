using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send and Send Options policies.
/// </summary>
public class SendPolicyRequirement : SendOptionsPolicyData, IPolicyRequirement
{
    public bool DisableSend { get; init; }

    public static SendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var filteredPolicies = policyDetails
            .ExemptRoles([OrganizationUserType.Owner, OrganizationUserType.Admin])
            .ExemptStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked])
            .ExemptProviders()
            .ToList();

        return filteredPolicies
            .GetPolicyType(PolicyType.SendOptions)
            .Select(p => p.GetDataModel<SendPolicyRequirement>())
            .Aggregate(
                new SendPolicyRequirement
                {
                    // Set Disable Send requirement in the initial seed
                    DisableSend = filteredPolicies.GetPolicyType(PolicyType.DisableSend).Any()
                },
                (result, data) => new SendPolicyRequirement
                {
                    DisableSend = result.DisableSend,
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });
    }
}
