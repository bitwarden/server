using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
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

namespace Bit.Core.Test.Services
{
    public class OrganizationServiceTests
    {
        // [Fact]
        [Theory, PaidOrganizationAutoData]
        public async Task OrgImportCreateNewUsers(SutProvider<OrganizationService> sutProvider, Guid userId,
            Organization org, List<OrganizationUserUserDetails> existingUsers, List<ImportedOrganizationUser> newUsers)
        {
            org.UseDirectory = true;
            org.Seats = 10;
            newUsers.Add(new ImportedOrganizationUser
            {
                Email = existingUsers.First().Email,
                ExternalId = existingUsers.First().ExternalId
            });
            var expectedNewUsersCount = newUsers.Count - 1;

            existingUsers.First().Type = OrganizationUserType.Owner;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
                .Returns(existingUsers);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
                .Returns(existingUsers.Count);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
                .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
            sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);

            await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 0));
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);

            // Create new users
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IMailService>().Received(1)
                .BulkSendOrganizationInviteEmailAsync(org.Name,
                Arg.Any<bool>(),
                Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(messages => messages.Count() == expectedNewUsersCount));

            // Send events
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IReferenceEventService>().Received(1)
                .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
                referenceEvent.Users == expectedNewUsersCount));
        }

        [Theory, PaidOrganizationAutoData]
        public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<OrganizationService> sutProvider,
            Guid userId, Organization org, List<OrganizationUserUserDetails> existingUsers,
            List<ImportedOrganizationUser> newUsers)
        {
            org.UseDirectory = true;
            org.Seats = newUsers.Count + existingUsers.Count + 1;
            var reInvitedUser = existingUsers.First();
            reInvitedUser.ExternalId = null;
            newUsers.Add(new ImportedOrganizationUser
            {
                Email = reInvitedUser.Email,
                ExternalId = reInvitedUser.Email,
            });
            var expectedNewUsersCount = newUsers.Count - 1;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
                .Returns(existingUsers);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
                .Returns(existingUsers.Count);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id)
                .Returns(new OrganizationUser { Id = reInvitedUser.Id });
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
                .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.ManageUsers(org.Id).Returns(true);

            await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default);
            await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
                .CreateAsync(default, default);

            // Upserted existing user
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

            // Created and invited new users
            await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
                .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IMailService>().Received(1)
                .BulkSendOrganizationInviteEmailAsync(org.Name,
                Arg.Any<bool>(),
                Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(messages => messages.Count() == expectedNewUsersCount));

            // Sent events
            await sutProvider.GetDependency<IEventService>().Received(1)
                .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                events.Where(e => e.Item2 == EventType.OrganizationUser_Invited).Count() == expectedNewUsersCount));
            await sutProvider.GetDependency<IReferenceEventService>().Received(1)
                .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
                referenceEvent.Users == expectedNewUsersCount));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_OrganizationIsNull_Throws(Guid organizationId, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(Task.FromResult<Organization>(null));
            var exception = await Assert.ThrowsAsync<NotFoundException>(
                () => sutProvider.Sut.UpgradePlanAsync(organizationId, upgrade));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_GatewayCustomIdIsNull_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            organization.GatewayCustomerId = string.Empty;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("no payment method", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_AlreadyInPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            upgrade.Plan = organization.PlanType;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("already on this plan", exception.Message);
        }

        [Theory, PaidOrganizationAutoData]
        public async Task UpgradePlan_UpgradeFromPaidPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("can only upgrade", exception.Message);
        }

        [Theory]
        [FreeOrganizationUpgradeAutoData]
        public async Task UpgradePlan_Passes(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);
            await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organization);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUser_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections, SutProvider<OrganizationService> sutProvider)
        {
            user.Id = default(Guid);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections));
            Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUser_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections));
            Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUser_Passes(
            OrganizationUser oldUserData,
            OrganizationUser newUserData,
            IEnumerable<SelectionReadOnly> collections,
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            newUserData.Id = oldUserData.Id;
            newUserData.UserId = oldUserData.UserId;
            newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId;
            organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
            organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
                .Returns(new List<OrganizationUser> { savingUser });
            currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

            await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_InvalidUser(OrganizationUser organizationUser, OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUserAsync(Guid.NewGuid(), organizationUser.Id, deletingUser.UserId));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_RemoveYourself(OrganizationUser deletingUser, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, deletingUser.Id, deletingUser.UserId));
            Assert.Contains("You cannot remove yourself.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_NonOwnerRemoveOwner(
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
            [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationUser.OrganizationId = deletingUser.OrganizationId;
            organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
            currentContext.OrganizationAdmin(deletingUser.OrganizationId).Returns(true);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId));
            Assert.Contains("Only owners can delete other owners.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_LastOwner(
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
            OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationUser.OrganizationId = deletingUser.OrganizationId;
            organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
            organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
                .Returns(new[] { organizationUser });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, null));
            Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUser_Success(
            OrganizationUser organizationUser,
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationUser.OrganizationId = deletingUser.OrganizationId;
            organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
            organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
            organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
                .Returns(new[] { deletingUser, organizationUser });
            currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

            await sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_FilterInvalid(OrganizationUser organizationUser, OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationUsers = new[] { organizationUser };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId));
            Assert.Contains("Users invalid.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_RemoveYourself(
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
            OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationUsers = new[] { deletingUser };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] { orgUser });

            var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
            Assert.Contains("You cannot remove yourself.", result[0].Item2);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_NonOwnerRemoveOwner(
            [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
            [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser orgUser2,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
            var organizationUsers = new[] { orgUser1 };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] { orgUser2 });

            var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
            Assert.Contains("Only owners can delete other owners.", result[0].Item2);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_LastOwner(
            [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            var organizationUsers = new[] { orgUser };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetManyByOrganizationAsync(orgUser.OrganizationId, OrganizationUserType.Owner).Returns(organizationUsers);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUsersAsync(orgUser.OrganizationId, organizationUserIds, null));
            Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_Success(
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
            [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
            var organizationUsers = new[] { orgUser1, orgUser2 };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
            organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
                .Returns(new[] { deletingUser, orgUser1 });
            currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

            await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateOrganizationKeysAsync_WithoutManageResetPassword_Throws(Guid orgId, string publicKey,
            string privateKey, SutProvider<OrganizationService> sutProvider)
        {
            var currentContext = Substitute.For<ICurrentContext>();
            currentContext.ManageResetPassword(orgId).Returns(false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => sutProvider.Sut.UpdateOrganizationKeysAsync(orgId, publicKey, privateKey));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Throws(Organization org, string publicKey,
            string privateKey, SutProvider<OrganizationService> sutProvider)
        {
            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.ManageResetPassword(org.Id).Returns(true);

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            organizationRepository.GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey));
            Assert.Contains("Organization Keys already exist", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Success(Organization org, string publicKey,
            string privateKey, SutProvider<OrganizationService> sutProvider)
        {
            org.PublicKey = null;
            org.PrivateKey = null;

            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            currentContext.ManageResetPassword(org.Id).Returns(true);

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            organizationRepository.GetByIdAsync(org.Id).Returns(org);

            await sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey);
        }

        [Theory]
        [InlinePaidOrganizationAutoData(PlanType.EnterpriseAnnually, new object[] { "Cannot set max seat autoscaling below seat count", 1, 0, 2 })]
        [InlinePaidOrganizationAutoData(PlanType.EnterpriseAnnually, new object[] { "Cannot set max seat autoscaling below seat count", 4, -1, 6 })]
        [InlineFreeOrganizationAutoData("Your plan does not allow seat autoscaling", 10, 0, null)]
        public async Task UpdateSubscription_BadInputThrows(string expectedMessage,
            int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
        {
            organization.Seats = currentSeats;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscription(organization.Id,
                seatAdjustment, maxAutoscaleSeats));

            Assert.Contains(expectedMessage, exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateSubscription_NoOrganization_Throws(Guid organizationId, SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization)null);

            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSubscription(organizationId, 0, null));
        }

        [Theory]
        [InlinePaidOrganizationAutoData(0, 100, null, true, "")]
        [InlinePaidOrganizationAutoData(0, 100, 100, true, "")]
        [InlinePaidOrganizationAutoData(0, null, 100, true, "")]
        [InlinePaidOrganizationAutoData(1, 100, null, true, "")]
        [InlinePaidOrganizationAutoData(1, 100, 100, false, "Cannot invite new users. Seat limit has been reached")]
        public async Task CanScale(int seatsToAdd, int? currentSeats, int? maxAutoscaleSeats,
            bool expectedResult, string expectedFailureMessage, Organization organization,
            SutProvider<OrganizationService> sutProvider)
        {
            organization.Seats = currentSeats;
            organization.MaxAutoscaleSeats = maxAutoscaleSeats;
            sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

            var (result, failureMessage) = sutProvider.Sut.CanScale(organization, seatsToAdd);

            if (expectedFailureMessage == string.Empty)
            {
                Assert.Empty(failureMessage);
            }
            else
            {
                Assert.Contains(expectedFailureMessage, failureMessage);
            }
            Assert.Equal(expectedResult, result);
        }

        [Theory, PaidOrganizationAutoData]
        public async Task CanScale_FailsOnSelfHosted(Organization organization,
            SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
            var (result, failureMessage) = sutProvider.Sut.CanScale(organization, 10);

            Assert.False(result);
            Assert.Contains("Cannot autoscale on self-hosted instance", failureMessage);
        }

        [Theory, PaidOrganizationAutoData]
        public async Task Delete_Success(Organization organization, SutProvider<OrganizationService> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

            await sutProvider.Sut.DeleteAsync(organization);

            await organizationRepository.Received().DeleteAsync(organization);
            await applicationCacheService.Received().DeleteOrganizationAbilityAsync(organization.Id);
        }

        [Theory, PaidOrganizationAutoData]
        public async Task Delete_Fails_KeyConnector(Organization organization, SutProvider<OrganizationService> sutProvider,
            SsoConfig ssoConfig)
        {
            ssoConfig.Enabled = true;
            ssoConfig.SetData(new SsoConfigurationData { KeyConnectorEnabled = true });
            var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

            ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteAsync(organization));

            Assert.Contains("You cannot delete an Organization that is using Key Connector.", exception.Message);

            await organizationRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
            await applicationCacheService.DidNotReceiveWithAnyArgs().DeleteOrganizationAbilityAsync(default);
        }
    }
}
