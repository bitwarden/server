using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.CollectionFixtures;
using Bit.Core.Test.Helpers.Factories;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class SecretRepositoryTests
    {
        [CiSkippedTheory, EfSecretAutoData]
        public async void CreateAsync_Works(
            Secret secret,
            Organization organization,
            SecretCompare equalityComparer,
            List<EfRepo.SecretRepository> suts,
            List<EfRepo.OrganizationRepository> efOrganizationRepos,
            SqlRepo.OrganizationRepository sqlOrgaizationRepo
            )
        {
            var createdSecrets = new List<Secret>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);
                if (i == (int)TestingDatabaseProviderOrder.SqlServer)
                {
                    var org = await sqlOrgaizationRepo.CreateAsync(organization);
                    secret.OrganizationId = org.Id;
                }
                else
                {
                    var org = await efOrganizationRepos[i].CreateAsync(organization);
                    sut.ClearChangeTracking();
                    secret.OrganizationId = org.Id;
                }
                await sut.CreateAsync(secret);
                sut.ClearChangeTracking();
                var result = await sut.GetByIdAsync(secret.Id);

                createdSecrets.Add(result);
            }
            var distinctItems = createdSecrets.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [CiSkippedTheory, EfSecretAutoData]
        public async void ReplaceAsync_Works(
            Secret secret,
            Organization organization,
            List<EfRepo.SecretRepository> suts,
            List<EfRepo.OrganizationRepository> efOrganizationRepos,
            SqlRepo.OrganizationRepository sqlOrgaizationRepo
            )
        {
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);
                if (i == (int)TestingDatabaseProviderOrder.SqlServer)
                {
                    var org = await sqlOrgaizationRepo.CreateAsync(organization);
                    secret.OrganizationId = org.Id;
                }
                else
                {
                    var org = await efOrganizationRepos[i].CreateAsync(organization);
                    sut.ClearChangeTracking();
                    secret.OrganizationId = org.Id;
                }
                await sut.CreateAsync(secret);
                sut.ClearChangeTracking();

                var result = await sut.GetByIdAsync(secret.Id);
                var originalNote = result.Note;
                result.Note = "test";
                await sut.ReplaceAsync(result);

                var afterReplace = await sut.GetByIdAsync(secret.Id);


                Assert.Equal(secret.Id, afterReplace.Id);
                Assert.Equal("test", afterReplace.Note);
                Assert.Equal(result.CreationDate, afterReplace.CreationDate);
                Assert.NotEqual(originalNote, afterReplace.Note);
            }
        }
    }
}
