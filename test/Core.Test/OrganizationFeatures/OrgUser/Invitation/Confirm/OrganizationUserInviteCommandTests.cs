using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;

namespace Bit.Core.Test.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    [SutProviderCustomize]
    public class OrganizationUserConfirmCommandTests
    {
        [Theory, BitAutoData]
        public async Task ConfirmUser_InvalidStatus(
            [OrganizationUser(OrganizationUserStatusType.Invited)]
            OrganizationUser orgUser, string key,
            SutProvider<OrganizationUserConfirmCommand> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory, BitAutoData]
        public async Task ConfirmUser_WrongOrganization(OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]
            OrganizationUser orgUser, string key,
            SutProvider<OrganizationUserConfirmCommand> sutProvider)
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
            SutProvider<OrganizationUserConfirmCommand> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserConfirmAccessPolicies>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
            accessPolicies.CanConfirmUserAsync(default, default, default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success);

            var result = await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key);
            var expected = orgUser.ConfirmUser(key);

            TestHelper.AssertPropertyEqual(expected, result);

            await accessPolicies.Received(1)
                .CanConfirmUserAsync(org, user, orgUser, Arg.Any<IEnumerable<OrganizationUser>>());
            await sutProvider.GetDependency<IOrganizationService>().Received(1)
                .DeleteAndPushUserRegistrationAsync(orgUser.OrganizationId, user.Id);
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventAsync(
                    Arg.Is<OrganizationUser>(u => u.Id == expected.Id),
                    EventType.OrganizationUser_Confirmed);
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
            SutProvider<OrganizationUserConfirmCommand> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var accessPolicies = sutProvider.GetDependency<IOrganizationUserConfirmAccessPolicies>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser1.OrganizationId = orgUser2.OrganizationId =
                orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser1.UserId = user1.Id;
            orgUser2.UserId = user2.Id;
            orgUser3.UserId = user3.Id;
            anotherOrgUser.UserId = user3.Id;
            var orgUsers = new[] { orgUser1, orgUser2, orgUser3 };

            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2, user3 });
            accessPolicies.CanConfirmUserAsync(default, default, default, default)
                .ReturnsForAnyArgs(AccessPolicyResult.Success,
                    AccessPolicyResult.Fail("User does not have two-step login enabled."),
                    AccessPolicyResult.Fail("User is a member of another organization."));

            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2, user3 });
            organizationUserRepository.GetManyByManyUsersAsync(default)
                .ReturnsForAnyArgs(new[] { orgUser1, orgUser2, orgUser3, anotherOrgUser });

            var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
            var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys);
            Assert.Contains("", result[0].Item2);
            Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
            Assert.Contains("User is a member of another organization.", result[2].Item2);
        }
    }
}
