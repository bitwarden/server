using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class PersonalOwnershipPolicyRequirement : IPolicyRequirement
{
    public bool DisablePersonalOwnership { get; init; }

    public static PersonalOwnershipPolicyRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
        => new()
        {
            DisablePersonalOwnership = userPolicyDetails
                .GetPolicyType(PolicyType.PersonalOwnership)
                .ExcludeOwnersAndAdmins()
                .ExcludeProviders()
                .ExcludeRevokedAndInvitedUsers()
                .Any()
        };
}
