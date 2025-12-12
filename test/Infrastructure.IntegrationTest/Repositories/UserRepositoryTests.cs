using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Microsoft.Data.SqlClient;
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

    [Theory, DatabaseData]
    public async Task DeleteManyAsync_Works(IUserRepository userRepository, IOrganizationUserRepository organizationUserRepository, IOrganizationRepository organizationRepository, ICollectionRepository collectionRepository, IGroupRepository groupRepository)
    {
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var user3 = await userRepository.CreateTestUserAsync();

        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user3);

        var group1 = await groupRepository.CreateTestGroupAsync(organization, "test-group-1");
        var group2 = await groupRepository.CreateTestGroupAsync(organization, "test-group-2");
        await groupRepository.UpdateUsersAsync(group1.Id, [orgUser1.Id]);
        await groupRepository.UpdateUsersAsync(group2.Id, [orgUser3.Id]);

        var collection1 = new Collection
        {
            OrganizationId = organization.Id,
            Name = "test-collection-1"
        };
        var collection2 = new Collection
        {
            OrganizationId = organization.Id,
            Name = "test-collection-2"
        };

        await collectionRepository.CreateAsync(
            collection1,
            groups: [new CollectionAccessSelection { Id = group1.Id, HidePasswords = false, ReadOnly = false, Manage = true }],
            users: [new CollectionAccessSelection { Id = orgUser1.Id, HidePasswords = false, ReadOnly = false, Manage = true }]);
        await collectionRepository.CreateAsync(collection2,
            groups: [new CollectionAccessSelection { Id = group2.Id, HidePasswords = false, ReadOnly = false, Manage = true }],
            users: [new CollectionAccessSelection { Id = orgUser3.Id, HidePasswords = false, ReadOnly = false, Manage = true }]);

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

        var collection1WithUsers = await collectionRepository.GetByIdWithPermissionsAsync(collection1.Id, null, true);
        var collection2WithUsers = await collectionRepository.GetByIdWithPermissionsAsync(collection2.Id, null, true);
        Assert.Empty(collection1WithUsers.Users); // Collection1 should have no users (orgUser1 was deleted)
        Assert.Single(collection2WithUsers.Users); // Collection2 should still have orgUser3 (not deleted)
        Assert.Single(collection2WithUsers.Users);
        Assert.Equal(orgUser3.Id, collection2WithUsers.Users.First().Id);

        var group1Users = await groupRepository.GetManyUserIdsByIdAsync(group1.Id);
        var group2Users = await groupRepository.GetManyUserIdsByIdAsync(group2.Id);

        Assert.Empty(group1Users); // Group1 should have no users (orgUser1 was deleted)
        Assert.Single(group2Users); // Group2 should still have orgUser3 (not deleted)
        Assert.Equal(orgUser3.Id, group2Users.First());
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_WhenUserHasDefaultUserCollections_MigratesToSharedCollection(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var defaultUserCollection = new Collection
        {
            Name = "Test Collection",
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        };
        await collectionRepository.CreateAsync(
            defaultUserCollection,
            groups: null,
            users: [new CollectionAccessSelection { Id = orgUser.Id, HidePasswords = false, ReadOnly = false, Manage = true }]);

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
        var user1 = await userRepository.CreateTestUserAsync();
        var user2 = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        var defaultUserCollection1 = new Collection
        {
            Name = "Test Collection 1",
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        };

        var defaultUserCollection2 = new Collection
        {
            Name = "Test Collection 2",
            Type = CollectionType.DefaultUserCollection,
            OrganizationId = organization.Id
        };

        await collectionRepository.CreateAsync(defaultUserCollection1, groups: null, users: [new CollectionAccessSelection { Id = orgUser1.Id, HidePasswords = false, ReadOnly = false, Manage = true }]);
        await collectionRepository.CreateAsync(defaultUserCollection2, groups: null, users: [new CollectionAccessSelection { Id = orgUser2.Id, HidePasswords = false, ReadOnly = false, Manage = true }]);

        await userRepository.DeleteManyAsync([user1, user2]);

        var deletedUser1 = await userRepository.GetByIdAsync(user1.Id);
        var deletedUser2 = await userRepository.GetByIdAsync(user2.Id);
        Assert.Null(deletedUser1);
        Assert.Null(deletedUser2);

        var updatedCollection1 = await collectionRepository.GetByIdAsync(defaultUserCollection1.Id);
        Assert.NotNull(updatedCollection1);
        Assert.Equal(CollectionType.SharedCollection, updatedCollection1.Type);
        Assert.Equal(user1.Email, updatedCollection1.DefaultUserCollectionEmail);

        var updatedCollection2 = await collectionRepository.GetByIdAsync(defaultUserCollection2.Id);
        Assert.NotNull(updatedCollection2);
        Assert.Equal(CollectionType.SharedCollection, updatedCollection2.Type);
        Assert.Equal(user2.Email, updatedCollection2.DefaultUserCollectionEmail);
    }

    [Theory, DatabaseData]
    public async Task SetKeyConnectorUserKey_UpdatesUserKey(IUserRepository userRepository, Database database)
    {
        var user = await userRepository.CreateTestUserAsync();

        const string keyConnectorWrappedKey = "key-connector-wrapped-user-key";

        var setKeyConnectorUserKeyDelegate = userRepository.SetKeyConnectorUserKey(user.Id, keyConnectorWrappedKey);

        await RunUpdateUserDataAsync(setKeyConnectorUserKeyDelegate, database);

        var updatedUser = await userRepository.GetByIdAsync(user.Id);

        Assert.NotNull(updatedUser);
        Assert.Equal(keyConnectorWrappedKey, updatedUser.Key);
        Assert.True(updatedUser.UsesKeyConnector);
        Assert.Equal(KdfType.Argon2id, updatedUser.Kdf);
        Assert.Equal(AuthConstants.ARGON2_ITERATIONS.Default, updatedUser.KdfIterations);
        Assert.Equal(AuthConstants.ARGON2_MEMORY.Default, updatedUser.KdfMemory);
        Assert.Equal(AuthConstants.ARGON2_PARALLELISM.Default, updatedUser.KdfParallelism);
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));
    }

    private static async Task RunUpdateUserDataAsync(UpdateUserData task, Database database)
    {
        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            await using var connection = new SqlConnection(database.ConnectionString);
            connection.Open();

            await using var transaction = connection.BeginTransaction();
            try
            {
                await task(connection, transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        else
        {
            await task();
        }
    }
}
