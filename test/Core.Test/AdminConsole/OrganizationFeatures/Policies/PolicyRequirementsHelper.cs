using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public static class SingleOrganizationPolicyRequirementTestFactory
{
    public static SingleOrganizationPolicyRequirement NoSinglePolicyOrganizationsForUser() => new([]);

    public static SingleOrganizationPolicyRequirement EnabledForTargetOrganization(Guid organizationId) =>
        new([
        new PolicyDetails
        {
            OrganizationId = organizationId,
            OrganizationUserId = Guid.NewGuid(),
            PolicyType = PolicyType.SingleOrg,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
            OrganizationUserType = OrganizationUserType.User
        }
    ]);

    public static SingleOrganizationPolicyRequirement EnabledForAnotherOrganization() =>
        new([
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                OrganizationUserId = Guid.NewGuid(),
                PolicyType = PolicyType.SingleOrg,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                OrganizationUserType = OrganizationUserType.User
            }
    ]);
}
