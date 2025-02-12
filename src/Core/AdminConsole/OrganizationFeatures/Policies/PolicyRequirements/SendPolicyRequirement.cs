using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SendPolicyRequirement : IPolicyRequirement
{
    public bool DisableSend { get; init; }
    public bool DisableHideEmail { get; init; }

    public static SendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var filteredPolicies = policyDetails
            .ExemptRoles([OrganizationUserType.Owner, OrganizationUserType.Admin])
            .ExemptStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked])
            .ExemptProviders()
            .ToList();

        return new SendPolicyRequirement
        {
            DisableSend = filteredPolicies
                .GetPolicyType(PolicyType.DisableSend)
                .Any(),

            DisableHideEmail = filteredPolicies
                .GetPolicyType(PolicyType.SendOptions)
                .Select(up => up.GetDataModel<SendOptionsPolicyData>())
                .Any(d => d.DisableHideEmail)
        };
    }
}
