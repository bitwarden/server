using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class TwoFactorAuthenticationRequirement : IRequirement
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
