using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_ShouldCreateDefaultCollection_WhenUsersDoNotHaveDefaultCollection(
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


        var affectedOrgUserIds = resultOrganizationUsers.Select(organizationUser => organizationUser.Id);
        var defaultCollectionName = $"default-name-{organization.Id}";

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_ShouldUpsertCreateDefaultCollection_ForUsersWithAndWithoutDefaultCollectionsExist(
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

        var newOrganizationUsers = new List<OrganizationUser>()
        {
            await CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
        };

        var affectedOrgUsers = newOrganizationUsers.Concat(arrangedOrganizationUsers);
        var affectedOrgUserIds = affectedOrgUsers.Select(organizationUser => organizationUser.Id);

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, arrangedOrganizationUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, affectedOrgUsers);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_ShouldNotCreateDefaultCollection_WhenUsersAlreadyHaveOne(
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

        var affectedOrgUserIds = resultOrganizationUsers.Select(organizationUser => organizationUser.Id);
        var defaultCollectionName = $"default-name-{organization.Id}";


        await CreateUsersWithExistingDefaultCollectionsAsync(collectionRepository, organization.Id, affectedOrgUserIds, defaultCollectionName, resultOrganizationUsers);

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(organization.Id, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resultOrganizationUsers, organization.Id);

        await CleanupAsync(organizationRepository, userRepository, organization, resultOrganizationUsers);
    }

    private static async Task CreateUsersWithExistingDefaultCollectionsAsync(ICollectionRepository collectionRepository,
        Guid organizationId, IEnumerable<Guid> affectedOrgUserIds, string defaultCollectionName,
        OrganizationUser[] resultOrganizationUsers)
    {
        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);

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
