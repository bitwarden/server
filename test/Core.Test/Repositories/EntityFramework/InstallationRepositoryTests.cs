using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.InstallationFixtures;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Xunit;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Linq;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class InstallationRepositoryTests
    {
        [CiSkippedTheory, EfInstallationAutoData]
        public async void CreateAsync_Works_DataMatches(
            Installation installation,
            InstallationCompare equalityComparer,
            List<EfRepo.InstallationRepository> suts,
            SqlRepo.InstallationRepository sqlInstallationRepo
            )
        {
            var savedInstallations = new List<Installation>();
            foreach (var sut in suts)
            {
                var postEfInstallation = await sut.CreateAsync(installation);
                sut.ClearChangeTracking();

                var savedInstallation = await sut.GetByIdAsync(postEfInstallation.Id);
                savedInstallations.Add(savedInstallation);
            }

            var sqlInstallation = await sqlInstallationRepo.CreateAsync(installation);
            var savedSqlInstallation = await sqlInstallationRepo.GetByIdAsync(sqlInstallation.Id);
            savedInstallations.Add(savedSqlInstallation);

            var distinctItems = savedInstallations.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }        
    }
}
