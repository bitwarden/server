using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryCreateTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_WithAccess_Works(
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

        var group1 = await groupRepository.CreateTestGroupAsync(organization);
        var group2 = await groupRepository.CreateTestGroupAsync(organization);

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        // Act
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

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Test Collection Name", actualCollection.Name);

        var groups = actualAccess.Groups.ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Single(groups, g => g.Id == group1.Id && g.Manage && g.HidePasswords && !g.ReadOnly);
        Assert.Single(groups, g => g.Id == group2.Id && !g.Manage && !g.HidePasswords && g.ReadOnly);

        var users = actualAccess.Users.ToArray();
        Assert.Equal(2, users.Length);
        Assert.Single(users, u => u.Id == orgUser1.Id && u.Manage && !u.HidePasswords && u.ReadOnly);
        Assert.Single(users, u => u.Id == orgUser2.Id && !u.Manage && u.HidePasswords && !u.ReadOnly);

        // Clean up data
        await userRepository.DeleteAsync(user1);
        await userRepository.DeleteAsync(user2);
        await organizationRepository.DeleteAsync(organization);
        await groupRepository.DeleteManyAsync([group1.Id, group2.Id]);
        await organizationUserRepository.DeleteManyAsync([orgUser1.Id, orgUser2.Id]);
    }

    /// <remarks>
    /// Makes sure that the sproc handles empty sets.
    /// </remarks>
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_WithNoAccess_Works(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var collection = new Collection
        {
            Name = "Test Collection Name",
            OrganizationId = organization.Id,
        };

        // Act
        await collectionRepository.CreateAsync(collection, [], []);

        // Assert
        var (actualCollection, actualAccess) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.NotNull(actualCollection);
        Assert.Equal("Test Collection Name", actualCollection.Name);

        Assert.Empty(actualAccess.Groups);
        Assert.Empty(actualAccess.Users);

        // Clean up
        await organizationRepository.DeleteAsync(organization);
    }
}
