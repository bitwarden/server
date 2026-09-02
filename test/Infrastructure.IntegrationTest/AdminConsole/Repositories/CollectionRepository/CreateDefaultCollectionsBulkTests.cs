using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;


public class CreateDefaultCollectionsBulkAsyncTests
{
    [Theory, DatabaseData]
    public async Task CreateDefaultCollectionsBulkAsync_CreatesDefaultCollections_Success(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        await CreateDefaultCollectionsSharedTests.CreatesDefaultCollections_Success(
            collectionRepository.CreateDefaultCollectionsBulkAsync,
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
    public async Task CreateDefaultCollectionsBulkAsync_IgnoresAllExistingUsers(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        await CreateDefaultCollectionsSharedTests.IgnoresAllExistingUsers(
            collectionRepository.CreateDefaultCollectionsBulkAsync,
            organizationRepository,
            userRepository,
            organizationUserRepository,
            collectionRepository);
    }
}
