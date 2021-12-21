using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.U2fFixtures;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class U2fRepositoryTests
    {

        [CiSkippedTheory, EfU2fAutoData]
        public async void CreateAsync_Works_DataMatches(
            U2f u2f,
            User user,
            U2fCompare equalityComparer,
            List<EfRepo.U2fRepository> suts,
            List<EfRepo.UserRepository> efUserRepos,
            SqlRepo.U2fRepository sqlU2fRepo,
            SqlRepo.UserRepository sqlUserRepo
            )
        {
            var savedU2fs = new List<U2f>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                var efUser = await efUserRepos[i].CreateAsync(user);
                sut.ClearChangeTracking();

                u2f.UserId = efUser.Id;
                var postEfU2f = await sut.CreateAsync(u2f);
                sut.ClearChangeTracking();

                var savedU2f = await sut.GetByIdAsync(postEfU2f.Id);
                savedU2fs.Add(savedU2f);
            }

            var sqlUser = await sqlUserRepo.CreateAsync(user);

            u2f.UserId = sqlUser.Id;
            var sqlU2f = await sqlU2fRepo.CreateAsync(u2f);
            var savedSqlU2f = await sqlU2fRepo.GetByIdAsync(sqlU2f.Id);
            savedU2fs.Add(savedSqlU2f);

            var distinctItems = savedU2fs.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }
    }
}
