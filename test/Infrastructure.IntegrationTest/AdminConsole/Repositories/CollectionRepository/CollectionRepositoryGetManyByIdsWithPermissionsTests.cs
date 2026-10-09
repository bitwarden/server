using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryGetManyByIdsWithPermissionsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_ReturnsEachCollectionsOwnPermissions(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var group = await groupRepository.CreateTestGroupAsync(organization);
        await groupRepository.AddGroupUsersByIdAsync(group.Id, [orgUser.Id], DateTime.UtcNow);

        var managedDirectly = new Collection { Name = "Direct", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(managedDirectly, groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        var managedViaGroup = new Collection { Name = "ViaGroup", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(managedViaGroup,
            groups: [new CollectionAccessSelection { Id = group.Id, Manage = true }], users: []);

        var notAssigned = new Collection { Name = "NotAssigned", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(notAssigned, groups: [], users: []);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [managedDirectly.Id, managedViaGroup.Id, notAssigned.Id], user.Id, includeAccessRelationships: false);

        // Assert
        Assert.Equal(3, results.Count);

        var direct = Single(results, managedDirectly.Id);
        Assert.Equal(organization.Id, direct.OrganizationId);
        Assert.True(direct.Assigned);
        Assert.True(direct.Manage);
        Assert.False(direct.Unmanaged);

        var viaGroup = Single(results, managedViaGroup.Id);
        Assert.True(viaGroup.Assigned);
        Assert.True(viaGroup.Manage);
        Assert.False(viaGroup.Unmanaged);

        var unassigned = Single(results, notAssigned.Id);
        Assert.False(unassigned.Assigned);
        Assert.False(unassigned.Manage);
        Assert.True(unassigned.Unmanaged);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_WhenACollectionIdDoesNotExist_ExcludesItFromTheResult(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [collection.Id, Guid.NewGuid()], user.Id, includeAccessRelationships: false);

        // Assert
        Assert.Equal(collection.Id, Assert.Single(results).Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_WhenAccessIsReadOnly_ReportsAssignedWithoutManage(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var collection = new Collection { Name = "ReadOnly", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, ReadOnly = true, Manage = false }]);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [collection.Id], user.Id, includeAccessRelationships: false);

        // Assert
        var result = Single(results, collection.Id);
        Assert.True(result.Assigned);
        Assert.False(result.Manage);
        Assert.True(result.ReadOnly);
        Assert.True(result.Unmanaged);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_WhenMembershipIsNotConfirmed_DoesNotGrantAssignedOrManage(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(organization, user);

        var collection = new Collection { Name = "Revoked", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [collection.Id], user.Id, includeAccessRelationships: false);

        // Assert
        var result = Single(results, collection.Id);
        Assert.False(result.Assigned);
        Assert.False(result.Manage);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_WhenOrganizationIsDisabled_DoesNotGrantAssignedOrManage(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var collection = new Collection { Name = "DisabledOrg", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(collection, groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        organization.Enabled = false;
        await organizationRepository.ReplaceAsync(organization);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [collection.Id], user.Id, includeAccessRelationships: false);

        // Assert
        var result = Single(results, collection.Id);
        Assert.False(result.Assigned);
        Assert.False(result.Manage);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByIdsWithPermissionsAsync_WithIncludeAccessRelationships_DoesNotMixCollectionsTogether(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IGroupRepository groupRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var firstCollection = new Collection { Name = "First", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(firstCollection, groups: [],
            users: [new CollectionAccessSelection { Id = orgUser.Id, Manage = true }]);

        var group = await groupRepository.CreateTestGroupAsync(organization);
        var secondCollection = new Collection { Name = "Second", OrganizationId = organization.Id };
        await collectionRepository.CreateAsync(secondCollection,
            groups: [new CollectionAccessSelection { Id = group.Id, Manage = true }], users: []);

        // Act
        var results = await collectionRepository.GetManyByIdsWithPermissionsAsync(
            [firstCollection.Id, secondCollection.Id], user.Id, includeAccessRelationships: true);

        // Assert
        var first = Single(results, firstCollection.Id);
        Assert.Equal(orgUser.Id, Assert.Single(first.Users).Id);
        Assert.Empty(first.Groups);

        var second = Single(results, secondCollection.Id);
        Assert.Equal(group.Id, Assert.Single(second.Groups).Id);
        Assert.Empty(second.Users);
    }

    private static CollectionAdminDetails Single(IEnumerable<CollectionAdminDetails> results, Guid collectionId)
        => Assert.Single(results, r => r.Id == collectionId);
}
