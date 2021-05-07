using System;
using System.Collections.Generic;
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
using Organization = Bit.Core.Models.Table.Organization;
using System.Linq;

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
            newUsers.Add(new ImportedOrganizationUser
            {
                Email = existingUsers.First().Email,
                ExternalId = existingUsers.First().ExternalId
            });
            var expectedNewUsersCount = newUsers.Count - 1;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
                .Returns(existingUsers);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
                .Returns(existingUsers.Count);

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
                Arg.Is<IEnumerable<(OrganizationUser, string)>>(messages => messages.Count() == expectedNewUsersCount));
            
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
                Arg.Is<IEnumerable<(OrganizationUser, string)>>(messages => messages.Count() == expectedNewUsersCount));

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
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
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
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.Id).Returns(new List<OrganizationUser> { invitor });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
            Assert.Contains("only an owner", exception.Message.ToLowerInvariant());
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.Custom,
            invitorUserType: (int)OrganizationUserType.Admin
        )]
        public async Task InviteUser_NonAdminConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.Id).Returns(new List<OrganizationUser> { invitor });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
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
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.UserId.Value).Returns(new List<OrganizationUser> { invitor });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
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
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.UserId.Value).Returns(new List<OrganizationUser> { invitor });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
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
            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var eventService = sutProvider.GetDependency<IEventService>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.UserId.Value).Returns(new List<OrganizationUser> { invitor });

            await sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite);
        }

        [Theory]
        [OrganizationInviteAutoData(
            inviteeUserType: (int)OrganizationUserType.User,
            invitorUserType: (int)OrganizationUserType.Custom
        )]
        public async Task InviteUser_Passes(Organization organization, OrganizationUserInvite invite,
            OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
        {
            invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });

            var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            var eventService = sutProvider.GetDependency<IEventService>();

            organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
            organizationUserRepository.GetManyByUserAsync(invitor.UserId.Value).Returns(new List<OrganizationUser> { invitor });

            await sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite);
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
        public async Task SaveUser_Passes(OrganizationUser oldUserData, OrganizationUser newUserData,
            IEnumerable<SelectionReadOnly> collections, OrganizationUser savingUser, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

            newUserData.Id = oldUserData.Id;
            newUserData.UserId = oldUserData.UserId;
            newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId;
            savingUser.Type = OrganizationUserType.Owner;
            organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
            organizationUserRepository.GetManyByUserAsync(savingUser.UserId.Value).Returns(new List<OrganizationUser> { savingUser });

            await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections);
        }
    }
}
