using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Vault.Entities;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using EfVaultRepo = Bit.Infrastructure.EntityFramework.Vault.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;
using SqlVaultRepo = Bit.Infrastructure.Dapper.Vault.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class FolderRepositoryTests
{
    [CiSkippedTheory, EfFolderAutoData]
    public async Task CreateAsync_Works_DataMatches(
        Folder folder,
        User user,
        FolderCompare equalityComparer,
        List<EfVaultRepo.FolderRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        SqlVaultRepo.FolderRepository sqlFolderRepo,
        SqlRepo.UserRepository sqlUserRepo
    )
    {
        var savedFolders = new List<Folder>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efUser = await efUserRepos[i].CreateAsync(user);
            sut.ClearChangeTracking();

            folder.UserId = efUser.Id;
            var postEfFolder = await sut.CreateAsync(folder);
            sut.ClearChangeTracking();

            var savedFolder = await sut.GetByIdAsync(folder.Id);
            savedFolders.Add(savedFolder);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);

        folder.UserId = sqlUser.Id;
        var sqlFolder = await sqlFolderRepo.CreateAsync(folder);
        savedFolders.Add(await sqlFolderRepo.GetByIdAsync(sqlFolder.Id));

        var distinctItems = savedFolders.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }
}
