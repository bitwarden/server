using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;

namespace Bit.Core.Test.OrganizationFeatures.OrgUser.Invitation.Invite
{
    [SutProviderCustomize]
    public class OrganizationUserInviteAccessPoliciesTests
    {
        [Theory, OrganizationInviteDataAutoData]
        public async Task InviteUser_NoEmails_Throws(Organization organization, OrganizationUser invitor,
            OrganizationUserInviteData invite, SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invite.Emails = null;

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail(), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.Admin,
            invitorUserType: (int)OrganizationUserType.Owner
        )]
        public async Task InviteUser_NoOwner_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
            sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default, default)
                .ReturnsForAnyArgs(false);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Organization must have at least one confirmed owner."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.Owner,
            invitorUserType: (int)OrganizationUserType.Admin
        )]
        public async Task InviteUser_NonOwnerConfiguringOwner_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Only an Owner can configure another Owner's account."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.Custom,
            invitorUserType: (int)OrganizationUserType.User
        )]
        public async Task InviteUser_NonAdminConfiguringAdmin_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationUser(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Only Owners and Admins can configure Custom accounts."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.Manager,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserWithoutManageUsersConfiguringUser_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = false },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, });

            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(false);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Your account does not have permission to manage users."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.Admin,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserConfiguringAdmin_Throws(Organization organization,
            OrganizationUserInviteData invite, OrganizationUser invitor,
            SutProvider<OrganizationUserInviteAccessPolicies> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, });

            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(true);

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Fail("Custom users can not manage Admins or Owners."), result);
        }

        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int)OrganizationUserType.User,
            invitorUserType: (int)OrganizationUserType.Owner
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

            var result = await sutProvider.Sut.CanInviteAsync(organization, new[] { invite }, invitor.UserId);

            Assert.Equal(AccessPolicyResult.Success, result);
        }
    }
}
