using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

[EfCollectionCustomize]
public class CollectionRepositoryTests
{
    [CiSkippedTheory, BitAutoData]
    public async Task CreateAsync_Works_DataMatches(
        Collection collection,
        Organization organization,
        CollectionCompare equalityComparer,
        List<EfRepo.CollectionRepository> suts,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        SqlRepo.CollectionRepository sqlCollectionRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo
    )
    {
        var savedCollections = new List<Collection>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
            sut.ClearChangeTracking();

            collection.OrganizationId = efOrganization.Id;
            var postEfCollection = await sut.CreateAsync(collection);
            sut.ClearChangeTracking();

            var savedCollection = await sut.GetByIdAsync(postEfCollection.Id);
            savedCollections.Add(savedCollection);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);
        collection.OrganizationId = sqlOrganization.Id;

        var sqlCollection = await sqlCollectionRepo.CreateAsync(collection);
        var savedSqlCollection = await sqlCollectionRepo.GetByIdAsync(sqlCollection.Id);
        savedCollections.Add(savedSqlCollection);

        var distinctItems = savedCollections.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }
}
