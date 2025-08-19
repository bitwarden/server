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
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            ResetPasswordKey = "resetpasswordkey1",
            AccessSecretsManager = false
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            ResetPasswordKey = "resetpasswordkey2",
            AccessSecretsManager = true
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
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            ResetPasswordKey = "resetpasswordkey1",
            AccessSecretsManager = false
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

    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByOrganizationAsync_vNext_WithoutGroupsAndCollections_ReturnsBasicUserDetails(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var id = Guid.NewGuid();

        var user1 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 1",
            Email = $"test1+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 2",
            Email = $"test2+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.Argon2id,
            KdfIterations = 4,
            KdfMemory = 5,
            KdfParallelism = 6
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            PrivateKey = "privatekey",
            PublicKey = "publickey",
            UseGroups = true,
            Enabled = true,
            UsePasswordManager = true
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            ResetPasswordKey = "resetpasswordkey1",
            AccessSecretsManager = false
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            ResetPasswordKey = "resetpasswordkey2",
            AccessSecretsManager = true
        });

        var responseModel = await organizationUserRepository.GetManyDetailsByOrganizationAsync_vNext(organization.Id, includeGroups: false, includeCollections: false);

        Assert.NotNull(responseModel);
        Assert.Equal(2, responseModel.Count);

        var user1Result = responseModel.FirstOrDefault(u => u.Id == orgUser1.Id);
        Assert.NotNull(user1Result);
        Assert.Equal(user1.Name, user1Result.Name);
        Assert.Equal(user1.Email, user1Result.Email);
        Assert.Equal(orgUser1.Status, user1Result.Status);
        Assert.Equal(orgUser1.Type, user1Result.Type);
        Assert.Equal(organization.Id, user1Result.OrganizationId);
        Assert.Equal(user1.Id, user1Result.UserId);
        Assert.Empty(user1Result.Groups);
        Assert.Empty(user1Result.Collections);

        var user2Result = responseModel.FirstOrDefault(u => u.Id == orgUser2.Id);
        Assert.NotNull(user2Result);
        Assert.Equal(user2.Name, user2Result.Name);
        Assert.Equal(user2.Email, user2Result.Email);
        Assert.Equal(orgUser2.Status, user2Result.Status);
        Assert.Equal(orgUser2.Type, user2Result.Type);
        Assert.Equal(organization.Id, user2Result.OrganizationId);
        Assert.Equal(user2.Id, user2Result.UserId);
        Assert.Empty(user2Result.Groups);
        Assert.Empty(user2Result.Collections);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByOrganizationAsync_vNext_WithGroupsAndCollections_ReturnsUserDetailsWithBoth(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        var id = Guid.NewGuid();
        var requestTime = DateTime.UtcNow;

        var user1 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 1",
            Email = $"test1+{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            PrivateKey = "privatekey",
            PublicKey = "publickey",
            UseGroups = true,
            Enabled = true
        });

        var group1 = await groupRepository.CreateAsync(new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Group 1",
            ExternalId = "external-group-1"
        });

        var group2 = await groupRepository.CreateAsync(new Group
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Group 2",
            ExternalId = "external-group-2"
        });

        var collection1 = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Collection 1",
            ExternalId = "external-collection-1",
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var collection2 = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "Test Collection 2",
            ExternalId = "external-collection-2",
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var defaultUserCollection = await collectionRepository.CreateAsync(new Collection
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "My Items",
            Type = CollectionType.DefaultUserCollection,
            DefaultUserCollectionEmail = user1.Email,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        // Create organization user with both groups and collections using CreateManyAsync
        var createOrgUserWithCollections = new List<CreateOrganizationUser>
        {
            new()
            {
                OrganizationUser = new OrganizationUser
                {
                    Id = CoreHelpers.GenerateComb(),
                    OrganizationId = organization.Id,
                    UserId = user1.Id,
                    Status = OrganizationUserStatusType.Confirmed,
                    Type = OrganizationUserType.Owner,
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
                        Id = collection2.Id,
                        ReadOnly = false,
                        HidePasswords = true,
                        Manage = true
                    },
                    new CollectionAccessSelection
                    {
                        Id = defaultUserCollection.Id,
                        ReadOnly = false,
                        HidePasswords = false,
                        Manage = true
                    }
                ],
                Groups = [group1.Id, group2.Id]
            }
        };

        await organizationUserRepository.CreateManyAsync(createOrgUserWithCollections);

        var responseModel = await organizationUserRepository.GetManyDetailsByOrganizationAsync_vNext(organization.Id, includeGroups: true, includeCollections: true);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);

        var user1Result = responseModel.First();

        Assert.Equal(user1.Name, user1Result.Name);
        Assert.Equal(user1.Email, user1Result.Email);
        Assert.Equal(organization.Id, user1Result.OrganizationId);
        Assert.Equal(user1.Id, user1Result.UserId);

        Assert.NotNull(user1Result.Groups);
        Assert.Equal(2, user1Result.Groups.Count());
        Assert.Contains(group1.Id, user1Result.Groups);
        Assert.Contains(group2.Id, user1Result.Groups);

        Assert.NotNull(user1Result.Collections);
        Assert.Equal(2, user1Result.Collections.Count());
        Assert.Contains(user1Result.Collections, c => c.Id == collection1.Id);
        Assert.Contains(user1Result.Collections, c => c.Id == collection2.Id);
        Assert.DoesNotContain(user1Result.Collections, c => c.Id == defaultUserCollection.Id);
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
        var requestTime = DateTime.UtcNow;

        var user1 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 1",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            CreationDate = requestTime,
            RevisionDate = requestTime,
            AccountRevisionDate = requestTime
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 2",
            Email = $"test+{id}@x-{domainName}", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            CreationDate = requestTime,
            RevisionDate = requestTime,
            AccountRevisionDate = requestTime
        });

        var user3 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 3",
            Email = $"test+{id}@{domainName}.example.com", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            CreationDate = requestTime,
            RevisionDate = requestTime,
            AccountRevisionDate = requestTime
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            Enabled = true,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var organizationDomain = new OrganizationDomain
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
            CreationDate = requestTime
        };
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetVerifiedDate();
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var responseModel = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);
        Assert.Equal(orgUser1.Id, responseModel.Single().Id);
        Assert.Equal(user1.Id, responseModel.Single().UserId);
        Assert.Equal(organization.Id, responseModel.Single().OrganizationId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationWithClaimedDomainsAsync_WithNoVerifiedDomain_ReturnsEmpty(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";
        var requestTime = DateTime.UtcNow;

        var user1 = await userRepository.CreateAsync(new User
        {
            Id = CoreHelpers.GenerateComb(),
            Name = "Test User 1",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            CreationDate = requestTime,
            RevisionDate = requestTime,
            AccountRevisionDate = requestTime
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = $"Test Org {id}",
            BillingEmail = user1.Email,
            Plan = "Test",
            Enabled = true,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        // Create domain but do NOT verify it
        var organizationDomain = new OrganizationDomain
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
            CreationDate = requestTime
        };
        organizationDomain.SetNextRunDate(12);
        // Note: NOT calling SetVerifiedDate()
        await organizationDomainRepository.CreateAsync(organizationDomain);

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            CreationDate = requestTime,
            RevisionDate = requestTime
        });

        var responseModel = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(responseModel);
        Assert.Empty(responseModel);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_PreservesDefaultCollections_WhenUpdatingCollectionAccess(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        // Create a regular collection and a default collection
        var regularCollection = await collectionRepository.CreateTestCollectionAsync(organization);

        // Manually create default collection since CreateTestCollectionAsync doesn't support type parameter
        var defaultCollection = new Collection
        {
            OrganizationId = organization.Id,
            Name = $"Default Collection {Guid.NewGuid()}",
            Type = CollectionType.DefaultUserCollection
        };
        await collectionRepository.CreateAsync(defaultCollection);

        var newCollection = await collectionRepository.CreateTestCollectionAsync(organization);

        // Set up initial collection access: user has access to both regular and default collections
        await organizationUserRepository.ReplaceAsync(orgUser, [
            new CollectionAccessSelection { Id = regularCollection.Id, ReadOnly = false, HidePasswords = false, Manage = false },
            new CollectionAccessSelection { Id = defaultCollection.Id, ReadOnly = false, HidePasswords = false, Manage = true }
        ]);

        // Verify initial state
        var (_, initialCollections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);
        Assert.Equal(2, initialCollections.Count);
        Assert.Contains(initialCollections, c => c.Id == regularCollection.Id);
        Assert.Contains(initialCollections, c => c.Id == defaultCollection.Id);

        // Act: Update collection access with only the new collection
        // This should preserve the default collection but remove the regular collection
        await organizationUserRepository.ReplaceAsync(orgUser, [
            new CollectionAccessSelection { Id = newCollection.Id, ReadOnly = false, HidePasswords = false, Manage = true }
        ]);

        // Assert
        var (actualOrgUser, actualCollections) = await organizationUserRepository.GetByIdWithCollectionsAsync(orgUser.Id);
        Assert.NotNull(actualOrgUser);
        Assert.Equal(2, actualCollections.Count); // Should have default collection + new collection

        // Default collection should be preserved
        var preservedDefaultCollection = actualCollections.FirstOrDefault(c => c.Id == defaultCollection.Id);
        Assert.NotNull(preservedDefaultCollection);
        Assert.True(preservedDefaultCollection.Manage); // Original permissions preserved

        // New collection should be added
        var addedNewCollection = actualCollections.FirstOrDefault(c => c.Id == newCollection.Id);
        Assert.NotNull(addedNewCollection);
        Assert.True(addedNewCollection.Manage);

        // Regular collection should be removed
        Assert.DoesNotContain(actualCollections, c => c.Id == regularCollection.Id);
    }
}
