using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Exceptions;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using System.Text.Json;

namespace Bit.Core.Test.Services
{
    public class OrganizationServiceTests
    {
        [Fact]
        public async Task OrgImportCreateNewUsers()
        {
            var orgRepo = Substitute.For<IOrganizationRepository>();
            var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
            var collectionRepo = Substitute.For<ICollectionRepository>();
            var userRepo = Substitute.For<IUserRepository>();
            var groupRepo = Substitute.For<IGroupRepository>();
            var dataProtector = Substitute.For<IDataProtector>();
            var mailService = Substitute.For<IMailService>();
            var pushNotService = Substitute.For<IPushNotificationService>();
            var pushRegService = Substitute.For<IPushRegistrationService>();
            var deviceRepo = Substitute.For<IDeviceRepository>();
            var licenseService = Substitute.For<ILicensingService>();
            var eventService = Substitute.For<IEventService>();
            var installationRepo = Substitute.For<IInstallationRepository>();
            var appCacheService = Substitute.For<IApplicationCacheService>();
            var paymentService = Substitute.For<IPaymentService>();
            var policyRepo = Substitute.For<IPolicyRepository>();
            var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
            var ssoUserRepo = Substitute.For<ISsoUserRepository>();
            var referenceEventService = Substitute.For<IReferenceEventService>();
            var globalSettings = Substitute.For<GlobalSettings>();

            var orgService = new OrganizationService(orgRepo, orgUserRepo, collectionRepo, userRepo,
                groupRepo, dataProtector, mailService, pushNotService, pushRegService, deviceRepo,
                licenseService, eventService, installationRepo, appCacheService, paymentService, policyRepo,
                ssoConfigRepo, ssoUserRepo, referenceEventService, globalSettings);

            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var org = new Organization
            {
                Id = id,
                Name = "Test Org",
                UseDirectory = true,
                UseGroups = true,
                Seats = 3
            };
            orgRepo.GetByIdAsync(id).Returns(org);

            var existingUsers = new List<OrganizationUserUserDetails>();
            existingUsers.Add(new OrganizationUserUserDetails
            {
                Id = Guid.NewGuid(),
                ExternalId = "a",
                Email = "a@test.com"
            });
            orgUserRepo.GetManyDetailsByOrganizationAsync(id).Returns(existingUsers);
            orgUserRepo.GetCountByOrganizationIdAsync(id).Returns(1);

            var newUsers = new List<Models.Business.ImportedOrganizationUser>();
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "a@test.com", ExternalId = "a" });
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "b@test.com", ExternalId = "b" });
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "c@test.com", ExternalId = "c" });
            await orgService.ImportAsync(id, userId, null, newUsers, null, false);

            await orgUserRepo.DidNotReceive().UpsertAsync(Arg.Any<OrganizationUser>());
            await orgUserRepo.Received(2).CreateAsync(Arg.Any<OrganizationUser>());
        }

        [Fact]
        public async Task OrgImportCreateNewUsersAndMarryExistingUser()
        {
            var orgRepo = Substitute.For<IOrganizationRepository>();
            var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
            var collectionRepo = Substitute.For<ICollectionRepository>();
            var userRepo = Substitute.For<IUserRepository>();
            var groupRepo = Substitute.For<IGroupRepository>();
            var dataProtector = Substitute.For<IDataProtector>();
            var mailService = Substitute.For<IMailService>();
            var pushNotService = Substitute.For<IPushNotificationService>();
            var pushRegService = Substitute.For<IPushRegistrationService>();
            var deviceRepo = Substitute.For<IDeviceRepository>();
            var licenseService = Substitute.For<ILicensingService>();
            var eventService = Substitute.For<IEventService>();
            var installationRepo = Substitute.For<IInstallationRepository>();
            var appCacheService = Substitute.For<IApplicationCacheService>();
            var paymentService = Substitute.For<IPaymentService>();
            var policyRepo = Substitute.For<IPolicyRepository>();
            var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
            var ssoUserRepo = Substitute.For<ISsoUserRepository>();
            var referenceEventService = Substitute.For<IReferenceEventService>();
            var globalSettings = Substitute.For<GlobalSettings>();

            var orgService = new OrganizationService(orgRepo, orgUserRepo, collectionRepo, userRepo,
                groupRepo, dataProtector, mailService, pushNotService, pushRegService, deviceRepo,
                licenseService, eventService, installationRepo, appCacheService, paymentService, policyRepo,
                ssoConfigRepo, ssoUserRepo, referenceEventService, globalSettings);

            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var org = new Organization
            {
                Id = id,
                Name = "Test Org",
                UseDirectory = true,
                UseGroups = true,
                Seats = 3
            };
            orgRepo.GetByIdAsync(id).Returns(org);

            var existingUserAId = Guid.NewGuid();
            var existingUsers = new List<OrganizationUserUserDetails>();
            existingUsers.Add(new OrganizationUserUserDetails
            {
                Id = existingUserAId,
                // No external id here
                Email = "a@test.com"
            });
            orgUserRepo.GetManyDetailsByOrganizationAsync(id).Returns(existingUsers);
            orgUserRepo.GetCountByOrganizationIdAsync(id).Returns(1);
            orgUserRepo.GetByIdAsync(existingUserAId).Returns(new OrganizationUser { Id = existingUserAId });

            var newUsers = new List<Models.Business.ImportedOrganizationUser>();
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "a@test.com", ExternalId = "a" });
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "b@test.com", ExternalId = "b" });
            newUsers.Add(new Models.Business.ImportedOrganizationUser { Email = "c@test.com", ExternalId = "c" });
            await orgService.ImportAsync(id, userId, null, newUsers, null, false);

            await orgUserRepo.Received(1).UpsertAsync(Arg.Any<OrganizationUser>());
            await orgUserRepo.Received(2).CreateAsync(Arg.Any<OrganizationUser>());
        }

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_OrganizationIsNull_Throws(Guid organizationId, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(Task.FromResult<Organization>(null));
            var exception = await Assert.ThrowsAsync<NotFoundException>(
                () => sutProvider.Sut.UpgradePlanAsync(organizationId, upgrade));
        }

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_GatewayCustomIdIsNull_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            organization.GatewayCustomerId = "";
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("no payment method", exception.Message);
        }

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_AlreadyInPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            upgrade.Plan = organization.PlanType;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("already on this plan", exception.Message);
        }

        [Theory, PaidOrganizationAutoDataAttribute]
        public async Task UpgradePlan_UpgradeFromPaidPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade));
            Assert.Contains("can only upgrade", exception.Message);
        }
         
        [Theory]
        [PaidOrganizationAutoData]
        public async Task UpgradePlan_Passes(Organization organization, OrganizationUpgrade upgrade,
                SutProvider<OrganizationService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);
            await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(organization);
        }

        [Theory]
        [OrganizationInviteAutoDataAttribute]
        public async Task InviteUser_NoEmails_Throws(Organization organization, OrganizationUser invitor,
            OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
        {
            invite.Emails = null;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            await Assert.ThrowsAsync<NotFoundException>(
                () => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, null, invite));
        }

        [Theory] 
        [OrganizationInviteAutoDataAttribute(
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
        [OrganizationInviteAutoDataAttribute(
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
        [OrganizationInviteAutoDataAttribute(
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
        [OrganizationInviteAutoDataAttribute(
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
        [OrganizationInviteAutoDataAttribute(
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

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
        public async Task SaveUser_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections, SutProvider<OrganizationService> sutProvider)
        {
            user.Id = default(Guid);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections));
            Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
        }

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
        public async Task SaveUser_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
            IEnumerable<SelectionReadOnly> collections, SutProvider<OrganizationService> sutProvider)
        {
            var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
            organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections));
            Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
        }

        [Theory, CustomAutoDataAttribute(typeof(SutProviderCustomization))]
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
