
using Bit.Core.Repositories;


namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.CollectionRepository;

public class CollectionRepositoryCreateManyJimmy
{
    [DatabaseTheory, DatabaseData]
    public async Task Jimmy_WIP(
        ICollectionRepository collectionRepository)
    {

        // Act
        await collectionRepository.CreateDefaultCollectionsAsync();


    }

    // Jimmy TODO: make sure to add the clean up data for the tests.
}
