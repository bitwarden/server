using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
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
        var organizationId = Guid.NewGuid();

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = organizationId,
            Name = "Test name",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test",
            BillingEmail = "billing@example.com"
        });

        var resulOrganizationUsers = await Task.WhenAll(
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization),
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
            );


        var affectedOrgUserIds = resulOrganizationUsers.Select(organizationUser => organizationUser.Id);
        var defaultCollectionName = $"default-name-{organizationId}";

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resulOrganizationUsers, organizationId);
    }


    [DatabaseTheory, DatabaseData]
    public async Task CreateDefaultCollectionsAsync_ShouldNotCreateDefaultCollection_WhenUsersAlreadyHaveOne(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = organizationId,
            Name = "Test name",
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Test",
            BillingEmail = "billing@example.com"
        });

        var resulOrganizationUsers = await Task.WhenAll(
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization),
            CreateUserForOrgAsync(userRepository, organizationUserRepository, organization)
        );

        var affectedOrgUserIds = resulOrganizationUsers.Select(organizationUser => organizationUser.Id);
        var defaultCollectionName = $"default-name-{organizationId}";


        await CreateUsersWithExistingDefaultCollections(collectionRepository, organizationId, affectedOrgUserIds, defaultCollectionName, resulOrganizationUsers);

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);

        // Assert
        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resulOrganizationUsers, organizationId);
    }

    private static async Task CreateUsersWithExistingDefaultCollections(ICollectionRepository collectionRepository,
        Guid organizationId, IEnumerable<Guid> affectedOrgUserIds, string defaultCollectionName,
        OrganizationUser[] resulOrganizationUsers)
    {
        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);

        await AssertAllUsersHaveOneDefaultCollectionAsync(collectionRepository, resulOrganizationUsers, organizationId);
    }

    private static async Task AssertAllUsersHaveOneDefaultCollectionAsync(ICollectionRepository collectionRepository,
        OrganizationUser[] resulOrganizationUsers, Guid organizationId)
    {
        foreach (var resulOrganizationUser in resulOrganizationUsers)
        {
            var collectionDetails = await collectionRepository.GetManyByUserIdAsync(resulOrganizationUser!.UserId.Value);
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
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        return orgUser;
    }

    // Jimmy TODO: make sure to add the clean up data for the tests.
}
