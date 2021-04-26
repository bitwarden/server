using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class CipherRepositoryTests
    {
        // TODO: delete this
        [Theory (Skip = "Run ad-hoc"), EfUserCipherAutoData]
        public async void RefreshDb(List<EfRepo.CipherRepository> suts)
        {
            foreach (var sut in suts)
            {
                await sut.RefreshDb();
            }
        }

        // this needs to test account revision dates somehow
        [CiSkippedTheory, EfUserCipherAutoData, EfOrganizationCipherAutoData]
        public async void CreateAsync_Works_DataMatches(Cipher cipher, User user, Organization org,
            CipherCompare equalityComparer, List<EfRepo.CipherRepository> suts, List<EfRepo.UserRepository> efUserRepos,
            List<EfRepo.OrganizationRepository> efOrgRepos, SqlRepo.CipherRepository sqlCipherRepo, 
            SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrgRepo)
        {
            var savedCiphers = new List<Cipher>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                var efUser = await efUserRepos[i].CreateAsync(user);
                sut.ClearChangeTracking();
                cipher.UserId = efUser.Id;

                if (cipher.OrganizationId.HasValue)
                {
                    var efOrg = await efOrgRepos[i].CreateAsync(org);
                    sut.ClearChangeTracking();
                    cipher.OrganizationId = efOrg.Id;
                }

                var postEfCipher = await sut.CreateAsync(cipher);
                sut.ClearChangeTracking();

                var savedCipher = await sut.GetByIdAsync(postEfCipher.Id);
                savedCiphers.Add(savedCipher);
            }

            var sqlUser = await sqlUserRepo.CreateAsync(user);
            cipher.UserId = sqlUser.Id;
            
            if (cipher.OrganizationId.HasValue)
            {
                var sqlOrg = await sqlOrgRepo.CreateAsync(org);
                cipher.OrganizationId = sqlOrg.Id;
            }

            var sqlCipher = await sqlCipherRepo.CreateAsync(cipher);
            var savedSqlCipher = await sqlCipherRepo.GetByIdAsync(sqlCipher.Id);
            savedCiphers.Add(savedSqlCipher);

            var distinctItems = savedCiphers.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }        
    }
}
