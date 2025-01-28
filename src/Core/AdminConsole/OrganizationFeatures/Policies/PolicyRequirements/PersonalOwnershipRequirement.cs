using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class PersonalOwnershipRequirement : IRequirement
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
