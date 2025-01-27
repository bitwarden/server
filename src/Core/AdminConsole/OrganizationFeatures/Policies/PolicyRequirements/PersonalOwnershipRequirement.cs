using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;

public class PersonalOwnershipRequirement
{
    public bool DisablePersonalOwnership { get; init; }

    public static PersonalOwnershipRequirement Create(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
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
