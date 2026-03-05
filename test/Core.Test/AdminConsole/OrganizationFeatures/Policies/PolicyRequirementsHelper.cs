using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public static class PolicyRequirementsFactory
{
    public static SingleOrganizationPolicyRequirement GetEnabledSingleOrgDetail(OrganizationUser organizationUser) =>
        new([
            new PolicyDetails
            {
                OrganizationId = organizationUser.OrganizationId,
                OrganizationUserId = organizationUser.Id,
                OrganizationUserStatus = organizationUser.Status,
                OrganizationUserType = organizationUser.Type,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

    public static SingleOrganizationPolicyRequirement GetEnabledSingleOrgDetails(OrganizationUser[] organizationUsers) =>
        new(organizationUsers.Select(x =>
            new PolicyDetails
            {
                OrganizationId = x.OrganizationId,
                OrganizationUserId = x.Id,
                OrganizationUserStatus = x.Status,
                OrganizationUserType = x.Type,
                PolicyType = PolicyType.SingleOrg
            }
        ));

    public static SingleOrganizationPolicyRequirement GetDisabledSingleOrganizationRequirement() => new([]);
}
