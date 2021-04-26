using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Linq;
using Bit.Core.Test.AutoFixture.GroupFixtures;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class GroupRepositoryTests
    {
        [CiSkippedTheory, EfGroupAutoData]
        public async void CreateAsync_Works_DataMatches(
            Group grp,
            Organization org,
            GroupCompare equalityComparer,
            List<EfRepo.GroupRepository> suts,
            List<EfRepo.OrganizationRepository> efOrganizationRepos,
            SqlRepo.GroupRepository sqlGroupRepo,
            SqlRepo.OrganizationRepository sqlOrganizationRepo
            )
        {
            var savedGroups = new List<Group>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                var efOrganization = await efOrganizationRepos[i].CreateAsync(org);
                sut.ClearChangeTracking();

                grp.OrganizationId = efOrganization.Id;
                var postEfGroup = await sut.CreateAsync(grp);
                sut.ClearChangeTracking();

                var savedGroup = await sut.GetByIdAsync(postEfGroup.Id);
                savedGroups.Add(savedGroup);
            }

            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);

            grp.OrganizationId = sqlOrganization.Id;
            var sqlGroup = await sqlGroupRepo.CreateAsync(grp);
            var savedSqlGroup = await sqlGroupRepo.GetByIdAsync(sqlGroup.Id);
            savedGroups.Add(savedSqlGroup);

            var distinctItems = savedGroups.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }        
    }
}
