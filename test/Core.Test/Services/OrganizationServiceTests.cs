using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;

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
            var globalSettings = Substitute.For<GlobalSettings>();

            var orgService = new OrganizationService(orgRepo, orgUserRepo, collectionRepo, userRepo,
                groupRepo, dataProtector, mailService, pushNotService, pushRegService, deviceRepo,
                licenseService, eventService, installationRepo, appCacheService, paymentService, globalSettings);

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
            await orgService.ImportAsync(id, userId, null, newUsers, null);

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
            var globalSettings = Substitute.For<GlobalSettings>();

            var orgService = new OrganizationService(orgRepo, orgUserRepo, collectionRepo, userRepo,
                groupRepo, dataProtector, mailService, pushNotService, pushRegService, deviceRepo,
                licenseService, eventService, installationRepo, appCacheService, paymentService, globalSettings);

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
            await orgService.ImportAsync(id, userId, null, newUsers, null);

            await orgUserRepo.Received(1).UpsertAsync(Arg.Any<OrganizationUser>());
            await orgUserRepo.Received(2).CreateAsync(Arg.Any<OrganizationUser>());
        }
    }
}
