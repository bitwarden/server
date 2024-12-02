using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using C = Bit.Core.Platform.Installations;
using D = Bit.Infrastructure.Dapper.Platform;
using Ef = Bit.Infrastructure.EntityFramework.Platform;

namespace Bit.Infrastructure.EFIntegration.Test.Platform;

public class InstallationRepositoryTests
{
    [CiSkippedTheory, EfInstallationAutoData]
    public async Task CreateAsync_Works_DataMatches(
        C.Installation installation,
        InstallationCompare equalityComparer,
        List<Ef.InstallationRepository> suts,
        D.InstallationRepository sqlInstallationRepo
        )
    {
        var savedInstallations = new List<C.Installation>();
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
