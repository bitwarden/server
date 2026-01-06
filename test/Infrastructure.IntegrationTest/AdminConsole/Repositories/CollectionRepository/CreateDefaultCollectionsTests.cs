using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsAsyncTests
{
    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_CreatesDefaultCollections_Success(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        await CreateDefaultCollectionsSharedTests.CreatesDefaultCollections_Success(
            collectionRepository.CreateDefaultCollectionsAsync,
            organizationRepository,
            userRepository,
            organizationUserRepository,
            collectionRepository);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_CreatesForNewUsersOnly_AndIgnoresExistingUsers(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        await CreateDefaultCollectionsSharedTests.CreatesForNewUsersOnly_AndIgnoresExistingUsers(
            collectionRepository.CreateDefaultCollectionsBulkAsync,
            organizationRepository,
            userRepository,
            organizationUserRepository,
            collectionRepository);
    }

    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_IgnoresAllExistingUsers(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        await CreateDefaultCollectionsSharedTests.IgnoresAllExistingUsers(
            collectionRepository.CreateDefaultCollectionsAsync,
            organizationRepository,
            userRepository,
            organizationUserRepository,
            collectionRepository);
    }
}
