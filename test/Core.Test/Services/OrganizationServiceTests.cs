using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Exceptions;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Organization = Bit.Core.Models.Table.Organization;
using OrganizationUser = Bit.Core.Models.Table.OrganizationUser;
using Policy = Bit.Core.Models.Table.Policy;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Core.Settings;
using AutoFixture.Xunit2;

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

        [Theory]
        [OrganizationInviteAutoData]
        public async Task InviteUser_NoEmails_Throws(Organization organization, OrganizationUser invitor,
            OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
        {
            invite.Emails = null;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            await Assert.ThrowsAsync<NotFoundException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        }
        
        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Admin,
            invitorUserType: (int)OrganizationUserType.Owner
        )]
        public async Task InviteUser_NoOwner_Throws(Organization organization, OrganizationUser invitor,
            OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
            Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Owner,
            invitorUserType: (int)OrganizationUserType.Admin
        )]
        public async Task InviteUser_NonOwnerConfiguringOwner_Throws(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            currentContext.OrganizationAdmin(organization.Id).Returns(true);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
            Assert.Contains("only an owner", exception.Message.ToLowerInvariant());
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Custom,
            invitorUserType: (int)OrganizationUserType.User
        )]
        public async Task InviteUser_NonAdminConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            currentContext.OrganizationUser(organization.Id).Returns(true);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
            Assert.Contains("only owners and admins", exception.Message.ToLowerInvariant());
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Manager,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserWithoutManageUsersConfiguringUser_Throws(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = false },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
            Assert.Contains("account does not have permission", exception.Message.ToLowerInvariant());
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Admin,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_CustomUserConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            currentContext.OrganizationCustom(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(true);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
            Assert.Contains("can not manage admins", exception.Message.ToLowerInvariant());
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.User,
            invitorUserType: (int)OrganizationUserType.Owner
        )]
        public async Task InviteUser_NoPermissionsObject_Passes(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            invite.Permissions = null;
            invitor.Status = OrganizationUserStatusType.Confirmed;
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
                .Returns(new [] {invitor});
            currentContext.OrganizationOwner(organization.Id).Returns(true);
            currentContext.ManageUsers(organization.Id).Returns(true);

            await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.User,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_Passes(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor,
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)]OrganizationUser owner,
            SutProvider<OrganizationService> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
                .Returns(new [] {owner});
            currentContext.ManageUsers(organization.Id).Returns(true);

            await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });
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
            [OrganizationUser(type: OrganizationUserType.Owner)]OrganizationUser savingUser,
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
            [OrganizationUser(type: OrganizationUserType.Owner)]OrganizationUser organizationUser,
            [OrganizationUser(type: OrganizationUserType.Admin)]OrganizationUser deletingUser,
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
            [OrganizationUser(type: OrganizationUserType.Owner)]OrganizationUser organizationUser,
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
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)]OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var currentContext = sutProvider.GetDependency<ICurrentContext>();
            
            organizationUser.OrganizationId = deletingUser.OrganizationId;
            organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
            organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
            organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
                .Returns(new[] {deletingUser, organizationUser});
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
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)]OrganizationUser orgUser,
            OrganizationUser deletingUser,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationUsers = new[] { deletingUser };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] {orgUser});

            var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
            Assert.Contains("You cannot remove yourself.", result[0].Item2);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_NonOwnerRemoveOwner(
            [OrganizationUser(type: OrganizationUserType.Admin)]OrganizationUser deletingUser,
            [OrganizationUser(type: OrganizationUserType.Owner)]OrganizationUser orgUser1,
            [OrganizationUser(OrganizationUserStatusType.Confirmed)]OrganizationUser orgUser2,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            
            orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
            var organizationUsers = new[] { orgUser1 };
            var organizationUserIds = organizationUsers.Select(u => u.Id);
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
            organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] {orgUser2});

            var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
            Assert.Contains("Only owners can delete other owners.", result[0].Item2);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsers_LastOwner(
            [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)]OrganizationUser orgUser,
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
            [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)]OrganizationUser deletingUser,
            [OrganizationUser(type: OrganizationUserType.Owner)]OrganizationUser orgUser1, OrganizationUser orgUser2,
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
                .Returns(new[] {deletingUser, orgUser1});
            currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

            await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_InvalidStatus(OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Invited)]OrganizationUser orgUser, string key,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var userService = Substitute.For<IUserService>();

            organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_WrongOrganization(OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser, string key,
            SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var userService = Substitute.For<IUserService>();

            organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(confirmingUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
            Assert.Contains("User not valid.", exception.Message);
        }

        [Theory]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, OrganizationUserType.Owner)]
        public async Task ConfirmUserToFree_AlreadyFreeAdminOrOwner_Throws(OrganizationUserType userType, Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
            string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var userService = Substitute.For<IUserService>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = PlanType.Free;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = userType;
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user});

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
            Assert.Contains("User can only be an admin of one free organization.", exception.Message);
        }

        [Theory]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.Custom, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.Custom, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseAnnually, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseAnnually, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseAnnually2019, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseAnnually2019, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseMonthly, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseMonthly, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseMonthly2019, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.EnterpriseMonthly2019, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.FamiliesAnnually, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.FamiliesAnnually, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.FamiliesAnnually2019, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.FamiliesAnnually2019, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsAnnually, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsAnnually, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsAnnually2019, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsAnnually2019, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsMonthly, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsMonthly, OrganizationUserType.Owner)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsMonthly2019, OrganizationUserType.Admin)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, PlanType.TeamsMonthly2019, OrganizationUserType.Owner)]
        public async Task ConfirmUserToNonFree_AlreadyFreeAdminOrOwner_DoesNotThrow(PlanType planType, OrganizationUserType orgUserType, Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
            string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var userService = Substitute.For<IUserService>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();

            org.PlanType = planType;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            orgUser.Type = orgUserType;
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
            organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

            await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService);

            await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
            await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.Name, user.Email);
            await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
        }


        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_SingleOrgPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.SingleOrg)]Policy singleOrgPolicy,
            string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();
            var userService = Substitute.For<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Accepted;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});
            organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] {orgUserAnotherOrg});
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user});
            policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] {singleOrgPolicy});

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
            Assert.Contains("User is a member of another organization.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_TwoFactorPolicy(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser, User user,
            OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.TwoFactorAuthentication)]Policy twoFactorPolicy,
            string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();
            var userService = Substitute.For<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});
            organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] {orgUserAnotherOrg});
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user});
            policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] {twoFactorPolicy});

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
            Assert.Contains("User does not have two-step login enabled.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUser_Success(Organization org, OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser, User user,
            [Policy(PolicyType.TwoFactorAuthentication)]Policy twoFactorPolicy,
            [Policy(PolicyType.SingleOrg)]Policy singleOrgPolicy, string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();
            var userService = Substitute.For<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser.UserId = user.Id;
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {orgUser});
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user});
            policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] {twoFactorPolicy, singleOrgPolicy});
            userService.TwoFactorIsEnabledAsync(user).Returns(true);

            await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUsers_Success(Organization org,
            OrganizationUser confirmingUser,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser1,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser2,
            [OrganizationUser(OrganizationUserStatusType.Accepted)]OrganizationUser orgUser3,
            OrganizationUser anotherOrgUser, User user1, User user2, User user3,
            [Policy(PolicyType.TwoFactorAuthentication)]Policy twoFactorPolicy,
            [Policy(PolicyType.SingleOrg)]Policy singleOrgPolicy, string key, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
            var userRepository = sutProvider.GetDependency<IUserRepository>();
            var userService = Substitute.For<IUserService>();

            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
            orgUser1.UserId = user1.Id;
            orgUser2.UserId = user2.Id;
            orgUser3.UserId = user3.Id;
            anotherOrgUser.UserId = user3.Id;
            var orgUsers = new[] {orgUser1, orgUser2, orgUser3};
            organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
            organizationRepository.GetByIdAsync(org.Id).Returns(org);
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {user1, user2, user3});
            policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] {twoFactorPolicy, singleOrgPolicy});
            userService.TwoFactorIsEnabledAsync(user1).Returns(true);
            userService.TwoFactorIsEnabledAsync(user2).Returns(false);
            userService.TwoFactorIsEnabledAsync(user3).Returns(true);
            organizationUserRepository.GetManyByManyUsersAsync(default)
                .ReturnsForAnyArgs(new[] {orgUser1, orgUser2, orgUser3, anotherOrgUser});

            var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
            var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys, confirmingUser.Id, userService);
            Assert.Contains("", result[0].Item2);
            Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
            Assert.Contains("User is a member of another organization.", result[2].Item2);
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
    }
}
