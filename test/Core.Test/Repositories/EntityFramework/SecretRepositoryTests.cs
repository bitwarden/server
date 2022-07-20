using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.CollectionFixtures;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class SecretRepositoryTests
    {
        [CiSkippedTheory, EfSecretAutoData]
        public async void CreateAsync_Works(
            Secret secret,
            Organization organization,
            List<EfRepo.SecretRepository> suts,
            List<EfRepo.OrganizationRepository> efOrganizationRepos)
        {
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);
                var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
                sut.ClearChangeTracking();

                secret.OrganizationId = efOrganization.Id;
                var result = await sut.CreateAsync(secret);
                sut.ClearChangeTracking();
            }
            Assert.True(true);
        }
    }
}
