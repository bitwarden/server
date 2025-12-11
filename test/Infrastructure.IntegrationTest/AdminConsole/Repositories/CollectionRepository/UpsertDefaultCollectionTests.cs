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

        // Act - Call method 20! times
        var tasks = Enumerable.Range(1, 20).Select(i => collectionRepository.UpsertDefaultCollectionAsync(
            organization.Id,
            orgUser.Id,
            defaultCollectionName));
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Single(results, r => r); // First call should create successfully; all other results are implicitly false

        // Verify only one collection exists
        var collectionDetails = await collectionRepository.GetManyByUserIdAsync(user.Id);
        Assert.Single(collectionDetails, c =>
            c.OrganizationId == organization.Id &&
            c.Type == CollectionType.DefaultUserCollection);
    }
}
