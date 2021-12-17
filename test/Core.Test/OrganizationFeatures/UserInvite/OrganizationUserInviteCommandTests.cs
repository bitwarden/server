using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.Mail;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.OrganizationFeatures.UserInvite;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;
using Bit.Core.Repositories;
using Bit.Core.Services.OrganizationServices.UserInvite;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Test.Common.AutoFixture.Attributes;
using Policy = Bit.Core.Models.Table.Policy;

namespace Bit.Core.Test.OrganizationFeatures.UserInvite
{
    [SutProviderCustomize]
    public class OrganizationUserInviteCommandTests
    {
        [Theory]
        [OrganizationInviteDataAutoData(
            inviteeUserType: (int) OrganizationUserType.User,
            invitorUserType: (int) OrganizationUserType.Custom
        )]
        public async Task InviteUser_Passes(Organization organization,
            IEnumerable<(OrganizationUserInviteData invite, string externalId)> invites,
            OrganizationUser invitor,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            // Autofixture will add collections for all of the invites, remove the first and for all the rest set all access false
            invites.First().invite.AccessAll = true;
            invites.First().invite.Collections = null;
            invites.Skip(1).ToList().ForEach(i => i.invite.AccessAll = false);
            var expectedInviteCount = invites.SelectMany(i => i.invite.Emails).Count();

            invitor.Permissions = JsonSerializer.Serialize(new Permissions() {ManageUsers = true},
                new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase,});

            sutProvider.GetDependency<IOrganizationUserInviteAccessPolicies>()
                .CanInviteAsync(organization, invites.Select(i => i.invite), default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<IOrganizationUserInviteService>().InviteUsersAsync(default, default, default)
                .ReturnsForAnyArgs(
                    invites.SelectMany(i => i.invite.Emails).Select(e => new OrganizationUser()).ToList());


            await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, invites);

            await sutProvider.GetDependency<IOrganizationUserMailer>().Received(1)
                .SendInvitesAsync(
                    Arg.Is<IEnumerable<(OrganizationUser invite, ExpiringToken token)>>(m =>
                        m.Count() == expectedInviteCount),
                    organization);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_InvalidStatus(
            [OrganizationUser(OrganizationUserStatusType.Invited)]
            OrganizationUser orgUser, string key,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_WrongOrganization(OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, string key,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(confirmingUser.OrganizationId, orgUser.Id, key));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_Success(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, User user, string key,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserInviteAccessPolicies>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user});
            accessPolicies.CanConfirmUserAsync(default, default, default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);

            var result = await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key);

            Assert.Equal(orgUser, result);

            await accessPolicies.Received(1)
                .CanConfirmUserAsync(org, user, orgUser, Arg.Any<IEnumerable<OrganizationUser>>());
            await sutProvider.GetDependency<IOrganizationUserService>().Received(1)
                .DeleteAndPushUserRegistrationAsync(orgUser.OrganizationId, user.Id);
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
            await sutProvider.GetDependency<IOrganizationUserMailer>().Received(1)
                .SendOrganizationConfirmedEmail(org, user);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUsers_Success(Organization org,
            OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser1,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser2,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser3,
            OrganizationUser anotherOrgUser, User user1, User user2, User user3, string key,
            SutProvider<OrganizationUserInviteCommand> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserInviteAccessPolicies>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser1.OrganizationId = orgUser2.OrganizationId =
                orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser1.UserId = user1.Id;
            orgUser2.UserId = user2.Id;
            orgUser3.UserId = user3.Id;
            anotherOrgUser.UserId = user3.Id;
            var orgUsers = new[] {orgUser1, orgUser2, orgUser3};

            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user1, user2, user3});
            accessPolicies.CanConfirmUserAsync(default, default, default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success,
                    AccessPolicyResult.Fail("User does not have two-step login enabled."),
                    AccessPolicyResult.Fail("User is a member of another organization."));

            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user1, user2, user3});
            organizationUserRepository.GetManyByManyUsersAsync(default)
                .ReturnsForAnyArgs(new[] {orgUser1, orgUser2, orgUser3, anotherOrgUser});
            
            var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
            var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys);
            Assert.Contains("", result[0].Item2);
            Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
            Assert.Contains("User is a member of another organization.", result[2].Item2);
        }
    }
}
