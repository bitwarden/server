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
        await AssertSempahoresCreatedAsync(collectionRepository, affectedOrgUserIds);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_CreatesForNewUsersOnly_WhenCallerFiltersExisting(
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

        // Act - Caller filters out existing users (new pattern)
        var existingSemaphores = await collectionRepository.GetDefaultCollectionSemaphoresAsync(affectedOrgUserIds);
        var usersNeedingCollections = affectedOrgUserIds.Except(existingSemaphores).ToList();
        await collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, usersNeedingCollections, defaultCollectionName);

        // Assert - All users now have exactly one collection
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, affectedOrgUsers, organization.Id);
        await AssertSempahoresCreatedAsync(collectionRepository, affectedOrgUserIds);

        await CleanupAsync(organizationRepository, userRepository, organization, affectedOrgUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_ThrowsException_WhenUsersAlreadyHaveOne(
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

        // Act - Try to create again, should throw database constraint exception
        await Assert.ThrowsAnyAsync<Exception>(() =>
            collectionRepository.CreateDefaultCollectionsBulkAsync(organization.Id, affectedOrgUserIds, defaultCollectionName));

        // Assert - Original collections should remain unchanged
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organization.Id);
        await AssertSempahoresCreatedAsync(collectionRepository, affectedOrgUserIds);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_ThrowsException_WhenDuplicatesNotFiltered(
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

        // Act - Try to create for both without filtering (incorrect usage)
        await Assert.ThrowsAnyAsync<Exception>(() =>
            collectionRepository.CreateDefaultCollectionsBulkAsync(
                organization.Id,
                [existingUser.Id, newUser.Id],
                defaultCollectionName));

        // Assert - Verify existing user still has collection
        var existingUserCollections = await collectionRepository.GetManyByUserIdAsync(existingUser.UserId!.Value);
        var existingUserDefaultCollection = existingUserCollections
            .SingleOrDefault(c => c.OrganizationId == organization.Id && c.Type == CollectionType.DefaultUserCollection);
        Assert.NotNull(existingUserDefaultCollection);

        // Verify new user does NOT have collection (transaction rolled back)
        var newUserCollections = await collectionRepository.GetManyByUserIdAsync(newUser.UserId!.Value);
        var newUserDefaultCollection = newUserCollections
            .FirstOrDefault(c => c.OrganizationId == organization.Id && c.Type == CollectionType.DefaultUserCollection);
        Assert.Null(newUserDefaultCollection);

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

    private static async Task AssertSempahoresCreatedAsync(ICollectionRepository collectionRepository,
        IEnumerable<Guid> organizationUserIds)
    {
        var organizationUserIdHashSet = organizationUserIds.ToHashSet();
        var semaphores = await collectionRepository.GetDefaultCollectionSemaphoresAsync(organizationUserIdHashSet);
        Assert.Equal(organizationUserIdHashSet, semaphores);
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
