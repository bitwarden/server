using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SendRequirement : IRequirement
{
    public bool DisableSend { get; init; }
    public bool DisableHideEmail { get; init; }

    public static SendRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    {
        var filteredPolicies = userPolicyDetails
            .ExcludeOwnersAndAdmins()
            .ExcludeRevokedAndInvitedUsers()
            .ToList();

        return new SendRequirement
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
