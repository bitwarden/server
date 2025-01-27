using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;

public class TwoFactorAuthenticationRequirement
{
    private IEnumerable<OrganizationUserPolicyDetails> PolicyDetails { get; }

    public TwoFactorAuthenticationRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    {
        PolicyDetails = userPolicyDetails
            .GetPolicyType(PolicyType.TwoFactorAuthentication)
            .ExcludeOwnersAndAdmins()
            .ExcludeProviders()
            .ToList();
    }

    public bool RequiredToJoinOrganization(Guid organizationId)
        => PolicyDetails.Any(x => x.OrganizationId == organizationId);

    public IEnumerable<Guid> OrganizationsRequiringTwoFactor
        => PolicyDetails
            .ExcludeRevokedAndInvitedUsers()
            .Select(x => x.OrganizationId);
}
