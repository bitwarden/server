using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Test.AutoFixture.CollectionFixtures;
using System.Linq;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class CollectionRepositoryTests
    {
        [CiSkippedTheory, EfCollectionAutoData]
        public async void CreateAsync_Works_DataMatches(
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
}
