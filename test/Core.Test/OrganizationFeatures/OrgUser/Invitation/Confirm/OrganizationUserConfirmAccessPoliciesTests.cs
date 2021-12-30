using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;
using Policy = Bit.Core.Models.Table.Policy;

namespace Bit.Core.Test.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    [SutProviderCustomize]
    public class OrganizationUserConfirmAccessPoliciesTests
    {
        [Theory]
        [BitAutoData(OrganizationUserType.Admin)]
        [BitAutoData(OrganizationUserType.Owner)]
        public async Task ConfirmUserToFree_AlreadyFreeAdminOrOwner_Throws(OrganizationUserType userType,
            Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            SutProvider<OrganizationUserConfirmAccessPolicies> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            org.PlanType = PlanType.Free;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = userType;
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] { orgUser });

            Assert.Equal(AccessPolicyResult.Fail("User can only be an admin of one free organization."), result);
        }

        [Theory]
        [BitAutoData(PlanType.Custom, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.Custom, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Owner)]
        [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Admin)]
        [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Owner)]
        public async Task ConfirmUserToNonFree_AlreadyFreeAdminOrOwner_DoesNotThrow(PlanType planType,
            OrganizationUserType orgUserType, Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            SutProvider<OrganizationUserConfirmAccessPolicies> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            org.PlanType = planType;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = orgUserType;
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] { orgUser });

            Assert.Equal(AccessPolicyResult.Success, result);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_SingleOrgPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
            SutProvider<OrganizationUserConfirmAccessPolicies> sutProvider)
        {
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            singleOrgPolicy.OrganizationId = org.Id;
            policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id, PolicyType.SingleOrg,
                OrganizationUserStatusType.Invited).Returns(new[] { singleOrgPolicy });

            var result =
                await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] { orgUserAnotherOrg, orgUser });

            Assert.Equal(AccessPolicyResult.Fail("User is a member of another organization."), result);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_TwoFactorPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.TwoFactorAuthentication)] Policy twoFactorPolicy,
            SutProvider<OrganizationUserConfirmAccessPolicies> sutProvider)
        {
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userService = sutProvider.GetDependency<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            twoFactorPolicy.OrganizationId = org.Id;
            policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id, PolicyType.TwoFactorAuthentication,
                OrganizationUserStatusType.Invited).Returns(new[] { twoFactorPolicy });
            userService.TwoFactorIsEnabledAsync(default).ReturnsForAnyArgs(false);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] { orgUser });

            Assert.Equal(AccessPolicyResult.Fail("User does not have two-step login enabled."), result);
        }
    }
}
