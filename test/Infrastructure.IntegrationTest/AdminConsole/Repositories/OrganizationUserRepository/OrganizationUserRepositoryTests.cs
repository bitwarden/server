﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class OrganizationUserRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user.Email, // TODO: EF does not enfore this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await organizationUserRepository.DeleteAsync(orgUser);

        var newUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(newUser);
        Assert.NotEqual(newUser.AccountRevisionDate, user.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await organizationUserRepository.DeleteManyAsync(new List<Guid>
        {
            orgUser1.Id,
            orgUser2.Id,
        });

        var updatedUser1 = await userRepository.GetByIdAsync(user1.Id);
        Assert.NotNull(updatedUser1);
        var updatedUser2 = await userRepository.GetByIdAsync(user2.Id);
        Assert.NotNull(updatedUser2);

        Assert.NotEqual(updatedUser1.AccountRevisionDate, user1.AccountRevisionDate);
        Assert.NotEqual(updatedUser2.AccountRevisionDate, user2.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyAccountRecoveryDetailsByOrganizationUserAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.Argon2id,
            KdfIterations = 4,
            KdfMemory = 5,
            KdfParallelism = 6
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey2",
        });

        var recoveryDetails = await organizationUserRepository.GetManyAccountRecoveryDetailsByOrganizationUserAsync(
            organization.Id,
            new[]
            {
                orgUser1.Id,
                orgUser2.Id,
            });

        Assert.NotNull(recoveryDetails);
        Assert.Equal(2, recoveryDetails.Count());
        Assert.Contains(recoveryDetails, r =>
            r.OrganizationUserId == orgUser1.Id &&
            r.Kdf == KdfType.PBKDF2_SHA256 &&
            r.KdfIterations == 1 &&
            r.KdfMemory == 2 &&
            r.KdfParallelism == 3 &&
            r.ResetPasswordKey == "resetpasswordkey1" &&
            r.EncryptedPrivateKey == "privatekey");
        Assert.Contains(recoveryDetails, r =>
            r.OrganizationUserId == orgUser2.Id &&
            r.Kdf == KdfType.Argon2id &&
            r.KdfIterations == 4 &&
            r.KdfMemory == 5 &&
            r.KdfParallelism == 6 &&
            r.ResetPasswordKey == "resetpasswordkey2" &&
            r.EncryptedPrivateKey == "privatekey");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByOrganizationAsync_WithIncludeCollections_ExcludesDefaultCollections(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user.Email,
            Plan = "Test",
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        // Create a regular collection
        var regularCollection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organization.Id,
            Name = "Regular Collection",
            Type = CollectionType.SharedCollection
        });

        // Create a default user collection
        var defaultCollection = await collectionRepository.CreateAsync(new Collection
        {
            OrganizationId = organization.Id,
            Name = "Default Collection",
            Type = CollectionType.DefaultUserCollection,
            DefaultUserCollectionEmail = user.Email
        });

        // Assign the organization user to both collections
        await organizationUserRepository.ReplaceAsync(orgUser, new List<CollectionAccessSelection>
        {
            new CollectionAccessSelection
            {
                Id = regularCollection.Id,
                ReadOnly = false,
                HidePasswords = false,
                Manage = true
            },
            new CollectionAccessSelection
            {
                Id = defaultCollection.Id,
                ReadOnly = false,
                HidePasswords = false,
                Manage = true
            }
        });

        // Get organization users with collections included
        var organizationUsers = await organizationUserRepository.GetManyDetailsByOrganizationAsync(
            organization.Id, includeGroups: false, includeCollections: true);

        Assert.NotNull(organizationUsers);
        Assert.Single(organizationUsers);

        var orgUserWithCollections = organizationUsers.First();
        Assert.NotNull(orgUserWithCollections.Collections);

        // Should only include the regular collection, not the default collection
        Assert.Single(orgUserWithCollections.Collections);
        Assert.Equal(regularCollection.Id, orgUserWithCollections.Collections.First().Id);
        Assert.DoesNotContain(orgUserWithCollections.Collections, c => c.Id == defaultCollection.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var responseModel = await organizationUserRepository.GetManyDetailsByUserAsync(user1.Id);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);
        var result = responseModel.Single();
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(user1.Id, result.UserId);
        Assert.Equal(orgUser1.Id, result.OrganizationUserId);
        Assert.Equal(organization.Name, result.Name);
        Assert.Equal(organization.UsePolicies, result.UsePolicies);
        Assert.Equal(organization.UseSso, result.UseSso);
        Assert.Equal(organization.UseKeyConnector, result.UseKeyConnector);
        Assert.Equal(organization.UseScim, result.UseScim);
        Assert.Equal(organization.UseGroups, result.UseGroups);
        Assert.Equal(organization.UseDirectory, result.UseDirectory);
        Assert.Equal(organization.UseEvents, result.UseEvents);
        Assert.Equal(organization.UseTotp, result.UseTotp);
        Assert.Equal(organization.Use2fa, result.Use2fa);
        Assert.Equal(organization.UseApi, result.UseApi);
        Assert.Equal(organization.UseResetPassword, result.UseResetPassword);
        Assert.Equal(organization.UseSecretsManager, result.UseSecretsManager);
        Assert.Equal(organization.UsePasswordManager, result.UsePasswordManager);
        Assert.Equal(organization.UsersGetPremium, result.UsersGetPremium);
        Assert.Equal(organization.UseCustomPermissions, result.UseCustomPermissions);
        Assert.Equal(organization.SelfHost, result.SelfHost);
        Assert.Equal(organization.Seats, result.Seats);
        Assert.Equal(organization.MaxCollections, result.MaxCollections);
        Assert.Equal(organization.MaxStorageGb, result.MaxStorageGb);
        Assert.Equal(organization.Identifier, result.Identifier);
        Assert.Equal(orgUser1.Key, result.Key);
        Assert.Equal(orgUser1.ResetPasswordKey, result.ResetPasswordKey);
        Assert.Equal(organization.PublicKey, result.PublicKey);
        Assert.Equal(organization.PrivateKey, result.PrivateKey);
        Assert.Equal(orgUser1.Status, result.Status);
        Assert.Equal(orgUser1.Type, result.Type);
        Assert.Equal(organization.Enabled, result.Enabled);
        Assert.Equal(organization.PlanType, result.PlanType);
        Assert.Equal(orgUser1.Permissions, result.Permissions);
        Assert.Equal(organization.SmSeats, result.SmSeats);
        Assert.Equal(organization.SmServiceAccounts, result.SmServiceAccounts);
        Assert.Equal(organization.LimitCollectionCreation, result.LimitCollectionCreation);
        Assert.Equal(organization.LimitCollectionDeletion, result.LimitCollectionDeletion);
        Assert.Equal(organization.AllowAdminAccessToAllCollectionItems, result.AllowAdminAccessToAllCollectionItems);
        Assert.Equal(organization.UseRiskInsights, result.UseRiskInsights);
        Assert.Equal(organization.UseAdminSponsoredFamilies, result.UseAdminSponsoredFamilies);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationWithClaimedDomainsAsync_WithVerifiedDomain_WithOneMatchingEmailDomain_ReturnsSingle(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@x-{domainName}", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user3 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@{domainName}.example.com", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
            UsePolicies = false,
            UseSso = false,
            UseKeyConnector = false,
            UseScim = false,
            UseGroups = false,
            UseDirectory = false,
            UseEvents = false,
            UseTotp = false,
            Use2fa = false,
            UseApi = false,
            UseResetPassword = false,
            UseSecretsManager = false,
            SelfHost = false,
            UsersGetPremium = false,
            UseCustomPermissions = false,
            Enabled = true,
            UsePasswordManager = false,
            LimitCollectionCreation = false,
            LimitCollectionDeletion = false,
            LimitItemDeletion = false,
            AllowAdminAccessToAllCollectionItems = false,
            UseRiskInsights = false,
            UseAdminSponsoredFamilies = false
        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetVerifiedDate();
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
            AccessSecretsManager = false
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var responseModel = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);
        Assert.Equal(orgUser1.Id, responseModel.Single().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateManyAsync_NoId_Works(IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var user3 = await userRepository.CreateTestUserAsync("user3");
        List<User> users = [user1, user2, user3];

        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"test-{Guid.NewGuid()}",
            BillingEmail = "billing@example.com", // TODO: EF does not enforce this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUsers = users.Select(u => new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = u.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner
        });

        var createdOrgUserIds = await organizationUserRepository.CreateManyAsync(orgUsers);

        var readOrgUsers = await organizationUserRepository.GetManyByOrganizationAsync(org.Id, null);
        var readOrgUserIds = readOrgUsers.Select(ou => ou.Id);

        Assert.Equal(createdOrgUserIds.ToHashSet(), readOrgUserIds.ToHashSet());
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateManyAsync_WithId_Works(IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var user3 = await userRepository.CreateTestUserAsync("user3");
        List<User> users = [user1, user2, user3];

        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"test-{Guid.NewGuid()}",
            BillingEmail = "billing@example.com", // TODO: EF does not enforce this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUsers = users.Select(u => new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),    // generate ID ahead of time
            OrganizationId = org.Id,
            UserId = u.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner
        });

        var createdOrgUserIds = await organizationUserRepository.CreateManyAsync(orgUsers);

        var readOrgUsers = await organizationUserRepository.GetManyByOrganizationAsync(org.Id, null);
        var readOrgUserIds = readOrgUsers.Select(ou => ou.Id);

        Assert.Equal(createdOrgUserIds.ToHashSet(), readOrgUserIds.ToHashSet());
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateManyAsync_WithCollectionAndGroup_SaveSuccessfully(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        var requestTime = DateTime.UtcNow;

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@test.com", // TODO: EF does not enfore this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl,
            CreationDate = requestTime
        });

        var collection1 = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Collection",
            ExternalId = "external-collection-1",
            CreationDate = requestTime,
            RevisionDate = requestTime
        });
        var collection2 = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Collection",
            ExternalId = "external-collection-1",
            CreationDate = requestTime,
            RevisionDate = requestTime
        });
        var collection3 = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Collection",
            ExternalId = "external-collection-1",
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        // Create a default user collection that should be excluded from admin results
        var defaultCollection = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "My Items",
            Type = CollectionType.DefaultUserCollection,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var group1 = await groupRepository.CreateAsync(new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Group",
            ExternalId = "external-group-1"
        });
        var group2 = await groupRepository.CreateAsync(new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Group",
            ExternalId = "external-group-1"
        });
        var group3 = await groupRepository.CreateAsync(new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Group",
            ExternalId = "external-group-1"
        });


        var orgUserCollection = new List<CreateOrganizationUser>
        {
            new()
            {
                OrganizationUser = new OrganizationUser
                {
                    Id = CoreHelpers.GenerateComb(),
                    OrganizationId = organization.Id,
                    Email = "test-user@test.com",
                    Status = OrganizationUserStatusType.Invited,
                    Type = OrganizationUserType.Owner,
                    ExternalId = "externalid-1",
                    Permissions = CoreHelpers.ClassToJsonData(new Permissions()),
                    AccessSecretsManager = false
                },
                Collections =
                [
                    new CollectionAccessSelection
                    {
                        Id = collection1.Id,
                        ReadOnly = true,
                        HidePasswords = false,
                        Manage = false
                    },
                    new CollectionAccessSelection
                    {
                        Id = defaultCollection.Id,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = true
                    }
                ],
                Groups = [group1.Id]
            },
            new()
            {
                OrganizationUser = new OrganizationUser
                {
                    Id = CoreHelpers.GenerateComb(),
                    OrganizationId = organization.Id,
                    Email = "test-user@test.com",
                    Status = OrganizationUserStatusType.Invited,
                    Type = OrganizationUserType.Owner,
                    ExternalId = "externalid-1",
                    Permissions = CoreHelpers.ClassToJsonData(new Permissions()),
                    AccessSecretsManager = false
                },
                Collections =
                [
                    new CollectionAccessSelection
                    {
                        Id = collection2.Id,
                        ReadOnly = true,
                        HidePasswords = false,
                        Manage = false
                    }
                ],
                Groups = [group2.Id]
            },
            new()
            {
                OrganizationUser = new OrganizationUser
                {
                    Id = CoreHelpers.GenerateComb(),
                    OrganizationId = organization.Id,
                    Email = "test-user@test.com",
                    Status = OrganizationUserStatusType.Invited,
                    Type = OrganizationUserType.Owner,
                    ExternalId = "externalid-1",
                    Permissions = CoreHelpers.ClassToJsonData(new Permissions()),
                    AccessSecretsManager = false
                },
                Collections =
                [
                    new CollectionAccessSelection
                    {
                        Id = collection3.Id,
                        ReadOnly = true,
                        HidePasswords = false,
                        Manage = false
                    }
                ],
                Groups = [group3.Id]
            }
        };

        await organizationUserRepository.CreateManyAsync(orgUserCollection);

        var orgUser1 = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUserCollection[0].OrganizationUser.Id);
        var group1Database = await groupRepository.GetManyIdsByUserIdAsync(orgUserCollection[0].OrganizationUser.Id);
        Assert.Equal(orgUserCollection[0].OrganizationUser.Id, orgUser1.OrganizationUser.Id);

        // Should only return the regular collection, not the default collection (even though both were assigned)
        Assert.Single(orgUser1.Collections);
        Assert.Equal(collection1.Id, orgUser1.Collections.First().Id);
        Assert.DoesNotContain(orgUser1.Collections, c => c.Id == defaultCollection.Id);
        Assert.Equal(group1.Id, group1Database.First());


        var orgUser2 = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUserCollection[1].OrganizationUser.Id);
        var group2Database = await groupRepository.GetManyIdsByUserIdAsync(orgUserCollection[1].OrganizationUser.Id);
        Assert.Equal(orgUserCollection[1].OrganizationUser.Id, orgUser2.OrganizationUser.Id);
        Assert.Equal(collection2.Id, orgUser2.Collections.First().Id);
        Assert.Equal(group2.Id, group2Database.First());

        var orgUser3 = await organizationUserRepository.GetDetailsByIdWithCollectionsAsync(orgUserCollection[2].OrganizationUser.Id);
        var group3Database = await groupRepository.GetManyIdsByUserIdAsync(orgUserCollection[2].OrganizationUser.Id);
        Assert.Equal(orgUserCollection[2].OrganizationUser.Id, orgUser3.OrganizationUser.Id);
        Assert.Equal(collection3.Id, orgUser3.Collections.First().Id);
        Assert.Equal(group3.Id, group3Database.First());
    }
}
