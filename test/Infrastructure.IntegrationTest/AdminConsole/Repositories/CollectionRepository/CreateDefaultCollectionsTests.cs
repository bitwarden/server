using Bit.Core.Repositories;


namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task SQL_Test(
        // IOrganizationRepository organizationRepository,
        // IUserRepository userRepository,
        // IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        //
        // var user = await userRepository.CreateAsync(new User
        // {
        //     Name = "Test User",
        //     Email = $"test+{Guid.NewGuid()}@email.com",
        //     ApiKey = "TEST",
        //     SecurityStamp = "stamp",
        // });
        //
        // var organization = await organizationRepository.CreateAsync(new Organization
        // {
        //     Name = "Test Org",
        //     PlanType = PlanType.EnterpriseAnnually,
        //     Plan = "Test Plan",
        //     BillingEmail = "billing@email.com"
        // });
        //
        // var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        // {
        //     OrganizationId = organization.Id,
        //     UserId = user.Id,
        //     Status = OrganizationUserStatusType.Confirmed,
        // });


        // Act
        var organizationId = Guid.Parse("75c909de-2624-461d-acd9-b2ff00e1c9aa");

        var useridEmailOrgUserId = Guid.Parse("676931B3-5479-403A-AFF1-B30D014F2A26");
        var useridUseridOrgUserId = Guid.Parse("D7AAC6DE-6958-4DF6-B22A-B30D0154D878");
        var defaultCollectionName = "default name";

        var affectedOrgUserIds = new[] { useridEmailOrgUserId, useridUseridOrgUserId };


        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);


    }

    [DatabaseTheory, DatabaseData]
    public async Task EF_Test(
        // IOrganizationRepository organizationRepository,
        // IUserRepository userRepository,
        // IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository)
    {
        //
        // var user = await userRepository.CreateAsync(new User
        // {
        //     Name = "Test User",
        //     Email = $"test+{Guid.NewGuid()}@email.com",
        //     ApiKey = "TEST",
        //     SecurityStamp = "stamp",
        // });
        //
        // var organization = await organizationRepository.CreateAsync(new Organization
        // {
        //     Name = "Test Org",
        //     PlanType = PlanType.EnterpriseAnnually,
        //     Plan = "Test Plan",
        //     BillingEmail = "billing@email.com"
        // });
        //
        // var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        // {
        //     OrganizationId = organization.Id,
        //     UserId = user.Id,
        //     Status = OrganizationUserStatusType.Confirmed,
        // });


        // Act
        var organizationId = Guid.Parse("7cbb0a89-f80d-4ce7-8eb5-b2ff00e1c9a8");

        var user1OrgUserId = Guid.Parse("75c909de-2624-461d-acd9-b2ff00e1c9aa");
        var user2OrgUserId = Guid.Parse("c76d2372-c449-4abf-9c89-b26801573811");
        var defaultCollectionName = "default name";

        var affectedOrgUserIds = new[] { user1OrgUserId, user2OrgUserId };


        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);


    }

    // Jimmy TODO: make sure to add the clean up data for the tests.
}
