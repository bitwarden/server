using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.UserInvite;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;
using Policy = Bit.Core.Models.Table.Policy;

namespace Bit.Core.Test.OrganizationFeatures.UserInvite
{
    [SutProviderCustomize]
    public class OrganizationUserInviteAccessPoliciesTests
    {
        [Theory]
        [OrganizationInviteDataAutoData]
        public async Task InviteUser_NoEmails_Throws(Organization organization, OrganizationUser invitor,
            OrganizationUserInviteData invite, SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invite.Emails = null;

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail(), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.Admin,
            invitorUserType: (int) OrganizationUserType.Owner
        )]
        public async Task InviteUser_NoOwner_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
            sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default, default)
                .ReturnsForAnyArgs(false);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Organization must have at least one confirmed owner."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.Owner,
            invitorUserType: (int) OrganizationUserType.Admin
        )]
        public async Task InviteUser_NonOwnerConfiguringOwner_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Only an Owner can configure another Owner's account."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.Custom,
            invitorUserType: (int) OrganizationUserType.User
        )]
        public async Task InviteUser_NonAdminConfiguringAdmin_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationUser(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Only Owners and Admins can configure Custom accounts."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.Manager,
            invitorUserType: (int) OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserWithoutManageUsersConfiguringUser_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() {ManageUsers = false},
                new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase,});

            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(false);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Your account does not have permission to manage users."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.Admin,
            invitorUserType: (int) OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserConfiguringAdmin_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() {ManageUsers = true},
                new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase,});

            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Custom users can not manage Admins or Owners."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.User,
            invitorUserType: (int) OrganizationUserType.Owner
        )]
        public async Task InviteUser_NoPermissionsObject_Passes(Organization organization,
            OrganizationUserInviteData invite,
            OrganizationUser invitor, SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invite.Permissions = null;
            invitor.Status = OrganizationUserStatusType.Confirmed;
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            currentContext.OrganizationOwner(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(true);
            sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default)
                .ReturnsForAnyArgs(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] {invite}, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Success, result);
        }


        [Theory]
        [BitAutoData(OrganizationUserType.Admin)]
        [BitAutoData(OrganizationUserType.Owner)]
        public async Task ConfirmUserToFree_AlreadyFreeAdminOrOwner_Throws(OrganizationUserType userType,
            Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            org.PlanType = PlanType.Free;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = userType;
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] {orgUser});

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
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            org.PlanType = planType;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = orgUserType;
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] {orgUser});

            Assert.Equal(AccessPolicyResult.Success, result);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_SingleOrgPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            singleOrgPolicy.OrganizationId = org.Id;
            policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id, PolicyType.SingleOrg,
                OrganizationUserStatusType.Invited).Returns(new[] {singleOrgPolicy});

            var result =
                await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] {orgUserAnotherOrg, orgUser});

            Assert.Equal(AccessPolicyResult.Fail("User is a member of another organization."), result);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_TwoFactorPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.TwoFactorAuthentication)] Policy twoFactorPolicy,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userService = sutProvider.GetDependency<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            twoFactorPolicy.OrganizationId = org.Id;
            policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id, PolicyType.TwoFactorAuthentication,
                OrganizationUserStatusType.Invited).Returns(new[] {twoFactorPolicy});
            userService.TwoFactorIsEnabledAsync(default).ReturnsForAnyArgs(false);

            var result = await sutProvider.Sut.CanConfirmUserAsync(org, user, orgUser, new[] {orgUser});

            Assert.Equal(AccessPolicyResult.Fail("User does not have two-step login enabled."), result);
        }
    }
}
