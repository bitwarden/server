using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryReplaceTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var user1 = await userRepository.CreateTestUserAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);

        var user2 = await userRepository.CreateTestUserAsync();
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        var user3 = await userRepository.CreateTestUserAsync();
        var orgUser3 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user3);

        var group1 = await groupRepository.CreateTestGroupAsync(organization);
        var group2 = await groupRepository.CreateTestGroupAsync(organization);
        var group3 = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        await collectionRepository.CreateAsync(collection,
            [
                new CollectionAccessSelection { Id = group1.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                new CollectionAccessSelection { Id = group2.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                new CollectionAccessSelection { Id = orgUser1.Id, Manage = true, HidePasswords = false, ReadOnly = true },
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = true, ReadOnly = false },
            ]
        );

        // Act
        collection.Name = "Updated Collection Name";

        await collectionRepository.ReplaceAsync(collection,
            [
                // Delete group1
                // Update group2:
                new CollectionAccessSelection { Id = group2.Id, Manage = true, HidePasswords = true, ReadOnly = false, },
                // Add group3:
                new CollectionAccessSelection { Id = group3.Id, Manage = false, HidePasswords = false, ReadOnly = true, },
            ],
            [
                // Delete orgUser1
                // Update orgUser2:
                new CollectionAccessSelection { Id = orgUser2.Id, Manage = false, HidePasswords = false, ReadOnly = true },
                // Add orgUser3:
                new CollectionAccessSelection { Id = orgUser3.Id, Manage = true, HidePasswords = false, ReadOnly = true },
            ]
        );

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Updated Collection Name", actualCollection.Name);

        var groups = actualAccess.Groups.ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Single(groups, g => g.Id == group2.Id && g.Manage && g.HidePasswords && !g.ReadOnly);
        Assert.Single(groups, g => g.Id == group3.Id && !g.Manage && !g.HidePasswords && g.ReadOnly);

        var users = actualAccess.Users.ToArray();

        Assert.Equal(2, users.Length);
        Assert.Single(users, u => u.Id == orgUser2.Id && !u.Manage && !u.HidePasswords && u.ReadOnly);
        Assert.Single(users, u => u.Id == orgUser3.Id && u.Manage && !u.HidePasswords && u.ReadOnly);

        // Clean up data
        // TODO: why doesn't delete many work?
        await userRepository.DeleteManyAsync([user1, user2, user3]);
        await organizationRepository.DeleteAsync(organization);
        await groupRepository.DeleteManyAsync([group1.Id, group2.Id, group3.Id]);
        await organizationUserRepository.DeleteManyAsync([orgUser1.Id, orgUser2.Id, orgUser3.Id]);
    }

    // TODO: replacing with an empty list of access to make sure we handle empty sets
}
