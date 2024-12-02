using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

class TwoFactorPolicyRequirementFactory : IPolicyRequirementFactory<TwoFactorPolicyRequirement>
{
    public PolicyType Type => PolicyType.TwoFactorAuthentication;

    public TwoFactorPolicyRequirement CreateRequirement(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        new(userPolicyDetails.Select(up => (up.OrganizationId, up.OrganizationUserStatus)));

    public bool EnforcePolicy(OrganizationUserPolicyDetails userPolicyDetails) =>
        // Note: we include the invited status so that we can enforce this before joining an org
        !userPolicyDetails.IsAdminType();
}

class TwoFactorPolicyRequirement(IEnumerable<(Guid orgId, OrganizationUserStatusType status)> twoFactorOrganizations) : IPolicyRequirement
{
    public bool CanJoinOrganization(Guid organizationId) => twoFactorOrganizations.Any(x => x.orgId == organizationId);

    public bool CanBeRestoredToOrganization(Guid organizationId) => CanJoinOrganization(organizationId);

    public IEnumerable<Guid> OrganizationsRequiringTwoFactor() =>
        twoFactorOrganizations
            .Where(x => x.status is OrganizationUserStatusType.Confirmed)
            .Select(x => x.orgId);
}
