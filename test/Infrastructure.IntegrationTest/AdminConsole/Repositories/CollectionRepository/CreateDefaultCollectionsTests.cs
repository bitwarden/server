using Bit.Core.AdminConsole.Collections;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsTests
{
    /// <summary>
    /// Test that CreateDefaultCollectionsAsync successfully creates default collections for new users
    /// with correct permissions
    /// </summary>
    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_CreatesDefaultCollections_Success(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user1 = await userRepository.CreateTestUserAsync("user1");
        var user2 = await userRepository.CreateTestUserAsync("user2");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser1 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user1);
        var orgUser2 = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user2);

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            [orgUser1.Id, orgUser2.Id],
            "My Items");

        // Assert
        var collectionsWithAccess = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organization.Id);
        var defaultCollections = collectionsWithAccess
            .Where(c => c.Item1.Type == CollectionType.DefaultUserCollection)
            .ToList();

        Assert.Equal(2, defaultCollections.Count);
        Assert.All(defaultCollections, c => Assert.Equal("My Items", c.Item1.Name));
        Assert.All(defaultCollections, c => Assert.Equal(organization.Id, c.Item1.OrganizationId));

        var semaphores = await collectionRepository.GetDefaultCollectionSemaphoresAsync([orgUser1.Id, orgUser2.Id]);
        Assert.Equal([orgUser1.Id, orgUser2.Id], semaphores);

        // Verify each user has exactly 1 collection with correct permissions
        var orgUser1Collection = Assert.Single(defaultCollections,
            c => c.Item2.Users.FirstOrDefault()?.Id == orgUser1.Id);

        Assert.Empty(orgUser1Collection.Item2.Groups);

        var orgUser1CollectionUser = orgUser1Collection.Item2.Users.Single();
        Assert.False(orgUser1CollectionUser.ReadOnly);
        Assert.False(orgUser1CollectionUser.HidePasswords);
        Assert.True(orgUser1CollectionUser.Manage);

        // Verify each user has exactly 1 collection with correct permissions
        var orgUser2Collection = Assert.Single(defaultCollections,
            c => c.Item2.Users.FirstOrDefault()?.Id == orgUser2.Id);

        Assert.Empty(orgUser2Collection.Item2.Groups);

        var orgUser2CollectionUser = orgUser2Collection.Item2.Users.Single();
        Assert.False(orgUser2CollectionUser.ReadOnly);
        Assert.False(orgUser2CollectionUser.HidePasswords);
        Assert.True(orgUser2CollectionUser.Manage);
    }

    /// <summary>
    /// Test that calling CreateDefaultCollectionsAsync multiple times does NOT create duplicates
    /// </summary>
    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_CalledMultipleTimesForSameOrganizationUser_Throws(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        // Arrange
        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        // Act - Call twice
        await collectionRepository.CreateDefaultCollectionsAsync(
            organization.Id,
            [orgUser.Id],
            "My Items");

        // Second call should throw specific exception and should not create duplicate
        await Assert.ThrowsAsync<DuplicateDefaultCollectionException>(() =>
            collectionRepository.CreateDefaultCollectionsAsync(
                organization.Id,
                [orgUser.Id],
                "My Items Duplicate"));

        // Assert - Only one collection should exist
        var collections = await collectionRepository.GetManyByOrganizationIdAsync(organization.Id);
        var defaultCollections = collections.Where(c => c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Single(defaultCollections);

        var semaphores = await collectionRepository.GetDefaultCollectionSemaphoresAsync([orgUser.Id]);
        Assert.Equal([orgUser.Id], semaphores);

        var access = await collectionRepository.GetManyUsersByIdAsync(defaultCollections.Single().Id);
        var userAccess = Assert.Single(access);
        Assert.Equal(orgUser.Id, userAccess.Id);
        Assert.False(userAccess.ReadOnly);
        Assert.False(userAccess.HidePasswords);
        Assert.True(userAccess.Manage);
    }
}
