
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsBulkTests
{
    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_ShouldCreateDefaultCollection_WhenUsersDoNotHaveDefaultCollection(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var resultOrganizationUsers = await Task.WhenAll(
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization),
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
            );

        var affectedOrgUserIds = resultOrganizationUsers.Select(organizationUser => organizationUser.Id).ToList();
        var defaultCollectionName = $"default-name-{organization.Id}";

        // Act
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_CreatesForNewUsersOnly_AutoFiltersExisting(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var arrangedOrganizationUsers = await Task.WhenAll(
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization),
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
        );

        var arrangedOrgUserIds = arrangedOrganizationUsers.Select(organizationUser => organizationUser.Id);
        var defaultCollectionName = $"default-name-{organization.Id}";

        await CreateUsersWithExistingDefaultCollectionsAsync(collectionRepository, organization.Id, arrangedOrgUserIds, defaultCollectionName, arrangedOrganizationUsers);

        var newOrganizationUsers = new List<OrganizationUser>
        {
            await CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
        };

        var affectedOrgUsers = newOrganizationUsers.Concat(arrangedOrganizationUsers);
        var affectedOrgUserIds = affectedOrgUsers.Select(organizationUser => organizationUser.Id).ToList();

        // Act - Pass all user IDs, method should auto-filter existing users
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert - All users now have exactly one collection
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, affectedOrgUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, affectedOrgUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_DoesNotCreateDuplicates_WhenUsersAlreadyHaveOne(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var resultOrganizationUsers = await Task.WhenAll(
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization),
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
        );

        var affectedOrgUserIds = resultOrganizationUsers.Select(organizationUser => organizationUser.Id).ToList();
        var defaultCollectionName = $"default-name-{organization.Id}";

        await CreateUsersWithExistingDefaultCollectionsAsync(collectionRepository, organization.Id, affectedOrgUserIds, defaultCollectionName, resultOrganizationUsers);

        // Act - Try to create again, should silently filter and not create duplicates
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert - Original collections should remain unchanged, still only one per user
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_AutoFilters_WhenMixedUsersProvided(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var existingUser = await CreateUserForOrgAsync(userRepository, organizationUserRepository, organization);
        var newUser = await CreateUserForOrgAsync(userRepository, organizationUserRepository, organization);
        var defaultCollectionName = $"default-name-{organization.Id}";

        // Create collection for existing user
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, [existingUser.Id], defaultCollectionName);

        // Act - Pass both users, method should auto-filter and only create for new user
        await collectionRepository.CreateDefaultCollectionsBulkAsync(
            organization.Id,
            [existingUser.Id, newUser.Id],
            defaultCollectionName);

        // Assert - Verify existing user still has exactly one collection
        var existingUserCollections = await collectionRepository.GetManyByUserIdAsync(existingUser.UserId!.Value);
        var existingUserDefaultCollections = existingUserCollections
            .Where(c => c.OrganizationId == organization.Id && c.Type == CollectionType.DefaultUserCollection)
            .ToList();
        Assert.Single(existingUserDefaultCollections);

        // Verify new user now has collection (was created)
        var newUserCollections = await collectionRepository.GetManyByUserIdAsync(newUser.UserId!.Value);
        var newUserDefaultCollection = newUserCollections
            .SingleOrDefault(c => c.OrganizationId == organization.Id && c.Type == CollectionType.DefaultUserCollection);
        Assert.NotNull(newUserDefaultCollection);

        await CleanupAsync(organizationRepository, userRepository, organization, [existingUser, newUser]);
    }

    private static async Task CreateUsersWithExistingDefaultCollectionsAsync(ICollectionRepository collectionRepository,
        Guid organizationId, IEnumerable<Guid> affectedOrgUserIds, string defaultCollectionName,
        OrganizationUser[] resultOrganizationUsers)
    {
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organizationId, affectedOrgUserIds, defaultCollectionName);

        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organizationId);
    }

    private static async Task AssertAllUsersHaveOneDefaultCollectionAsync(ICollectionRepository collectionRepository,
        IEnumerable<OrganizationUser> organizationUsers, Guid organizationId)
    {
        foreach (var organizationUser in organizationUsers)
        {
            var collectionDetails = await collectionRepository.GetManyByUserIdAsync(organizationUser!.UserId.Value);
            var defaultCollection = collectionDetails
                .SingleOrDefault(collectionDetail =>
                    collectionDetail.OrganizationId == organizationId
                    && collectionDetail.Type == CollectionType.DefaultUserCollection);

            Assert.NotNull(defaultCollection);
        }
    }

    private static async Task<OrganizationUser> CreateUserForOrgAsync(IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository, Organization organization)
    {
        var user = await userRepository.CreateTestUserAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        return orgUser;
    }

    private static async Task CleanupAsync(IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        Organization organization,
        IEnumerable<OrganizationUser> organizationUsers)
    {
        await organizationRepository.DeleteAsync(organization);

        await userRepository.DeleteManyAsync(
            organizationUsers
                .Where(organizationUser => organizationUser.UserId != null)
                .Select(organizationUser => new User() { Id = organizationUser.UserId.Value })
        );
    }
}
