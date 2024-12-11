using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class InstallationRepositoryTests
{
    [CiSkippedTheory, EfInstallationAutoData]
    public async Task CreateAsync_Works_DataMatches(
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
