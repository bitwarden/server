
using Bit.Core.Repositories;


namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CreateDefaultCollectionsTests
{
    [DatabaseTheory, DatabaseData]
    public async Task WIP(
        ICollectionRepository collectionRepository)
    {

        // Act
        var organizationId = Guid.Parse("C8D71195-CA4F-473F-80E6-B2AB010F35EF");

        var useridEmail = Guid.Parse("676931B3-5479-403A-AFF1-B30D014F2A26");
        var useridUserid = Guid.Parse("D7AAC6DE-6958-4DF6-B22A-B30D0154D878");
        var defaultCollectionName = "default name";

        var affectedOrgUserIds = new[] { useridEmail, useridUserid };


        await collectionRepository.CreateDefaultCollectionsAsync(organizationId, affectedOrgUserIds, defaultCollectionName);


    }

    // Jimmy TODO: make sure to add the clean up data for the tests.
}
