using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

/// <summary>
/// These tests show that the write paths accept only the group ids and the member ids that belong to the
/// collection's organization.
/// </summary>
public class CollectionRepositoryOrganizationScopingTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_WithGroupAndUserFromAnotherOrganization_DoesNotAddThem(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();

        var group = await groupRepository.CreateTestGroupAsync(organization);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);

        var user = await userRepository.CreateTestUserAsync();
        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var otherUser = await userRepository.CreateTestUserAsync();
        var otherOrganizationUser =
            await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, otherUser);

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, [], []);

        // Act
        await collectionRepository.ReplaceAsync(collection,
            [
                new CollectionAccessSelection { Id = group.Id, Manage = true },
                new CollectionAccessSelection { Id = otherGroup.Id, Manage = true },
            ],
            [
                new CollectionAccessSelection { Id = organizationUser.Id, Manage = true },
                new CollectionAccessSelection { Id = otherOrganizationUser.Id, Manage = true },
            ]);

        // Assert
        var (_, access) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.Equal(group.Id, Assert.Single(access.Groups).Id);
        Assert.Equal(organizationUser.Id, Assert.Single(access.Users).Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateUsersAsync_WithUserFromAnotherOrganization_DoesNotAddIt(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();

        var user = await userRepository.CreateTestUserAsync();
        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var otherUser = await userRepository.CreateTestUserAsync();
        var otherOrganizationUser =
            await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, otherUser);

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, [], []);

        // Act
        await collectionRepository.UpdateUsersAsync(collection.Id,
            [
                new CollectionAccessSelection { Id = organizationUser.Id, Manage = true },
                new CollectionAccessSelection { Id = otherOrganizationUser.Id, Manage = true },
            ]);

        // Assert
        var (_, access) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.Equal(organizationUser.Id, Assert.Single(access.Users).Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateOrUpdateAccessForManyAsync_WithGroupAndUserFromAnotherOrganization_DoesNotAddThem(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();

        var group = await groupRepository.CreateTestGroupAsync(organization);
        var otherGroup = await groupRepository.CreateTestGroupAsync(otherOrganization);

        var user = await userRepository.CreateTestUserAsync();
        var organizationUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var otherUser = await userRepository.CreateTestUserAsync();
        var otherOrganizationUser =
            await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, otherUser);

        var collection = new Collection { Name = "Test Collection", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, [], []);

        // Act
        await collectionRepository.CreateOrUpdateAccessForManyAsync(
            organization.Id,
            [collection.Id],
            [
                new CollectionAccessSelection { Id = organizationUser.Id, Manage = true },
                new CollectionAccessSelection { Id = otherOrganizationUser.Id, Manage = true },
            ],
            [
                new CollectionAccessSelection { Id = group.Id, Manage = true },
                new CollectionAccessSelection { Id = otherGroup.Id, Manage = true },
            ],
            DateTime.UtcNow);

        // Assert
        var (_, access) = await collectionRepository.GetByIdWithAccessAsync(collection.Id);

        Assert.Equal(group.Id, Assert.Single(access.Groups).Id);
        Assert.Equal(organizationUser.Id, Assert.Single(access.Users).Id);
    }
}
