using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class UpsertDefaultCollectionTests
{
    [Theory, DatabaseData]
    public async Task UpsertDefaultCollectionAsync_ShouldCreateCollection_WhenUserDoesNotHaveDefaultCollection(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var defaultCollectionName = $"My Items - {organization.Id}";

        // Act
        var wasCreated = await collectionRepository.UpsertDefaultCollectionAsync(
            organization.Id,
            orgUser.Id,
            defaultCollectionName);

        // Assert
        Assert.True(wasCreated);

        var collectionDetails = await collectionRepository.GetManyByUserIdAsync(user.Id);
        var defaultCollection = collectionDetails.SingleOrDefault(c =>
            c.OrganizationId == organization.Id &&
            c.Type == CollectionType.DefaultUserCollection);

        Assert.NotNull(defaultCollection);
        Assert.Equal(defaultCollectionName, defaultCollection.Name);
        Assert.True(defaultCollection.Manage);
        Assert.False(defaultCollection.ReadOnly);
        Assert.False(defaultCollection.HidePasswords);

        // Cleanup
        await CleanupAsync(organizationRepository, userRepository, organization, orgUser);
    }

    [Theory, DatabaseData]
    public async Task UpsertDefaultCollectionAsync_ShouldReturnFalse_WhenUserAlreadyHasDefaultCollection(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var defaultCollectionName = $"My Items - {organization.Id}";

        // Create initial collection
        var firstWasCreated = await collectionRepository.UpsertDefaultCollectionAsync(
            organization.Id,
            orgUser.Id,
            defaultCollectionName);

        // Act - Call again with same parameters
        var secondWasCreated = await collectionRepository.UpsertDefaultCollectionAsync(
            organization.Id,
            orgUser.Id,
            defaultCollectionName);

        // Assert
        Assert.True(firstWasCreated);
        Assert.False(secondWasCreated);

        // Verify only one default collection exists
        var collectionDetails = await collectionRepository.GetManyByUserIdAsync(user.Id);
        var defaultCollections = collectionDetails.Where(c =>
            c.OrganizationId == organization.Id &&
            c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Single(defaultCollections);

        // Cleanup
        await CleanupAsync(organizationRepository, userRepository, organization, orgUser);
    }

    [Theory, DatabaseData]
    public async Task UpsertDefaultCollectionAsync_ShouldBeIdempotent_WhenCalledMultipleTimes(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var defaultCollectionName = $"My Items - {organization.Id}";

        // Act - Call method 5 times
        var results = new List<bool>();
        for (int i = 0; i < 5; i++)
        {
            var wasCreated = await collectionRepository.UpsertDefaultCollectionAsync(
                organization.Id,
                orgUser.Id,
                defaultCollectionName);
            results.Add(wasCreated);
        }

        // Assert
        Assert.True(results[0]); // First call should create
        Assert.All(results.Skip(1), wasCreated => Assert.False(wasCreated)); // Rest should return false

        // Verify only one collection exists
        var collectionDetails = await collectionRepository.GetManyByUserIdAsync(user.Id);
        var defaultCollections = collectionDetails.Where(c =>
            c.OrganizationId == organization.Id &&
            c.Type == CollectionType.DefaultUserCollection).ToList();

        Assert.Single(defaultCollections);

        // Cleanup
        await CleanupAsync(organizationRepository, userRepository, organization, orgUser);
    }

    [Theory, DatabaseData]
    public async Task UpsertDefaultCollectionAsync_ShouldSetCorrectPermissions_ForNewCollection(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);
        var defaultCollectionName = $"My Items - {organization.Id}";

        // Act
        await collectionRepository.UpsertDefaultCollectionAsync(
            organization.Id,
            orgUser.Id,
            defaultCollectionName);

        // Assert
        var collectionDetails = await collectionRepository.GetManyByUserIdAsync(user.Id);
        var defaultCollection = collectionDetails.Single(c =>
            c.OrganizationId == organization.Id &&
            c.Type == CollectionType.DefaultUserCollection);

        Assert.True(defaultCollection.Manage);
        Assert.False(defaultCollection.ReadOnly);
        Assert.False(defaultCollection.HidePasswords);

        // Cleanup
        await CleanupAsync(organizationRepository, userRepository, organization, orgUser);
    }

    private static async Task CleanupAsync(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        Organization organization,
        OrganizationUser organizationUser)
    {
        await organizationRepository.DeleteAsync(organization);

        if (organizationUser.UserId != null)
        {
            await userRepository.DeleteAsync(new User { Id = organizationUser.UserId.Value });
        }
    }
}
