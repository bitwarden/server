using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class UserRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_Works(IUserRepository userRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        await userRepository.DeleteAsync(user);

        var deletedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.Null(deletedUser);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_Works(IUserRepository userRepository, IOrganizationUserRepository organizationUserRepository, IOrganizationRepository organizationRepository)
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

        var user3 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 3",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user3.Email, // TODO: EF does not enforce this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await userRepository.DeleteManyAsync(new List<User>
        {
            user1,
            user2
        });

        var deletedUser1 = await userRepository.GetByIdAsync(user1.Id);
        var deletedUser2 = await userRepository.GetByIdAsync(user2.Id);
        var notDeletedUser3 = await userRepository.GetByIdAsync(user3.Id);

        var orgUser1Deleted = await organizationUserRepository.GetByIdAsync(user1.Id);

        var notDeletedOrgUsers = await organizationUserRepository.GetManyByUserAsync(user3.Id);

        Assert.Null(deletedUser1);
        Assert.Null(deletedUser2);
        Assert.NotNull(notDeletedUser3);

        Assert.Null(orgUser1Deleted);
        Assert.NotNull(notDeletedOrgUsers);
        Assert.True(notDeletedOrgUsers.Count > 0);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_WhenUserHasDefaultUserCollections_MigratesToSharedCollection(
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
            Email = user.Email
        });

        var defaultUserCollection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            Id = user.Id,
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        });

        // Create the CollectionUser entry for the defaultUserCollection
        await collectionRepository.UpdateUsersAsync(defaultUserCollection.Id, new List<CollectionAccessSelection>()
        {
            new CollectionAccessSelection
            {
                Id = orgUser.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            },
        });

        await userRepository.DeleteAsync(user);

        var deletedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.Null(deletedUser);

        var updatedCollection = await collectionRepository.GetByIdAsync(defaultUserCollection.Id);
        Assert.NotNull(updatedCollection);
        Assert.Equal(CollectionType.SharedCollection, updatedCollection.Type);
        Assert.Equal(user.Email, updatedCollection.DefaultUserCollectionEmail);
    }

    [Theory, DatabaseData]
    public async Task DeleteManyAsync_WhenUsersHaveDefaultUserCollections_MigratesToSharedCollection(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test1+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test2+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email,
            Plan = "Test",
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Email = user1.Email
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Email = user2.Email
        });

        var defaultUserCollection1 = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection 1",
            Id = user1.Id,
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        });

        var defaultUserCollection2 = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection 2",
            Id = user2.Id,
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        });

        // Create the CollectionUser entries
        await collectionRepository.UpdateUsersAsync(defaultUserCollection1.Id, new List<CollectionAccessSelection>()
        {
            new CollectionAccessSelection
            {
                Id = orgUser1.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            },
        });

        await collectionRepository.UpdateUsersAsync(defaultUserCollection2.Id, new List<CollectionAccessSelection>()
        {
            new CollectionAccessSelection
            {
                Id = orgUser2.Id,
                HidePasswords = false,
                ReadOnly = false,
                Manage = true
            },
        });

        // Act
        await userRepository.DeleteManyAsync(new[] { user1, user2 });

        // Assert
        var deletedUser1 = await userRepository.GetByIdAsync(user1.Id);
        var deletedUser2 = await userRepository.GetByIdAsync(user2.Id);
        Assert.Null(deletedUser1);
        Assert.Null(deletedUser2);

        // Both collections should be migrated to SharedCollection
        var updatedCollection1 = await collectionRepository.GetByIdAsync(defaultUserCollection1.Id);
        Assert.NotNull(updatedCollection1);
        Assert.Equal(CollectionType.SharedCollection, updatedCollection1.Type);
        Assert.Equal(user1.Email, updatedCollection1.DefaultUserCollectionEmail);

        var updatedCollection2 = await collectionRepository.GetByIdAsync(defaultUserCollection2.Id);
        Assert.NotNull(updatedCollection2);
        Assert.Equal(CollectionType.SharedCollection, updatedCollection2.Type);
        Assert.Equal(user2.Email, updatedCollection2.DefaultUserCollectionEmail);
    }
}
