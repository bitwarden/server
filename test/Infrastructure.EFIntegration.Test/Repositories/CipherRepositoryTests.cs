using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Bit.Test.Common.AutoFixture.Attributes;
using LinqToDB;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class CipherRepositoryTests
{
    [Theory(Skip = "Run ad-hoc"), EfUserCipherCustomize, BitAutoData]
    public async void RefreshDb(List<EfRepo.CipherRepository> suts)
    {
        foreach (var sut in suts)
        {
            await sut.RefreshDb();
        }
    }

    [CiSkippedTheory, EfUserCipherCustomize, BitAutoData]
    public Task UserCipher_CreateAsync_Works_DataMatches(Cipher cipher, User user, Organization org,
        CipherCompare equalityComparer, List<EfRepo.CipherRepository> suts, List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlRepo.CipherRepository sqlCipherRepo,
        SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrgRepo) => CreateAsync_Works_DataMatches(
            cipher, user, org, equalityComparer, suts, efUserRepos, efOrgRepos, sqlCipherRepo, sqlUserRepo, sqlOrgRepo);

    [CiSkippedTheory, EfOrganizationCipherCustomize, BitAutoData]
    public Task OrganizationCipher_CreateAsync_Works_DataMatches(Cipher cipher, User user, Organization org,
        CipherCompare equalityComparer, List<EfRepo.CipherRepository> suts, List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlRepo.CipherRepository sqlCipherRepo,
        SqlRepo.UserRepository sqlUserRepo, SqlRepo.OrganizationRepository sqlOrgRepo) => CreateAsync_Works_DataMatches(
            cipher, user, org, equalityComparer, suts, efUserRepos, efOrgRepos, sqlCipherRepo, sqlUserRepo, sqlOrgRepo);

    private async Task CreateAsync_Works_DataMatches(Cipher cipher, User user, Organization org,
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

    [CiSkippedTheory, EfUserCipherCustomize, BitAutoData]
    public async void CreateAsync_BumpsUserAccountRevisionDate(Cipher cipher, User user, List<EfRepo.CipherRepository> suts, List<EfRepo.UserRepository> efUserRepos)
    {
        var bumpedUsers = new List<User>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efUser = await efUserRepos[i].CreateAsync(user);
            efUserRepos[i].ClearChangeTracking();
            cipher.UserId = efUser.Id;
            cipher.OrganizationId = null;

            var postEfCipher = await sut.CreateAsync(cipher);
            sut.ClearChangeTracking();

            var bumpedUser = await efUserRepos[i].GetByIdAsync(efUser.Id);
            bumpedUsers.Add(bumpedUser);
        }

        Assert.True(bumpedUsers.All(u => u.AccountRevisionDate.ToShortDateString() == DateTime.UtcNow.ToShortDateString()));
    }

    [CiSkippedTheory, EfOrganizationCipherCustomize, BitAutoData]
    public async void CreateAsync_BumpsOrgUserAccountRevisionDates(Cipher cipher, List<User> users,
        List<OrganizationUser> orgUsers, Collection collection, Organization org, List<EfRepo.CipherRepository> suts, List<EfRepo.UserRepository> efUserRepos, List<EfRepo.OrganizationRepository> efOrgRepos,
        List<EfRepo.OrganizationUserRepository> efOrgUserRepos, List<EfRepo.CollectionRepository> efCollectionRepos)
    {
        var savedCiphers = new List<Cipher>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efUsers = await efUserRepos[i].CreateMany(users);
            efUserRepos[i].ClearChangeTracking();
            var efOrg = await efOrgRepos[i].CreateAsync(org);
            efOrgRepos[i].ClearChangeTracking();

            cipher.OrganizationId = efOrg.Id;

            collection.OrganizationId = efOrg.Id;
            var efCollection = await efCollectionRepos[i].CreateAsync(collection);
            efCollectionRepos[i].ClearChangeTracking();

            IEnumerable<object>[] lists = { efUsers, orgUsers };
            var maxOrgUsers = lists.Min(l => l.Count());

            orgUsers = orgUsers.Take(maxOrgUsers).ToList();
            efUsers = efUsers.Take(maxOrgUsers).ToList();

            for (var j = 0; j < maxOrgUsers; j++)
            {
                orgUsers[j].OrganizationId = efOrg.Id;
                orgUsers[j].UserId = efUsers[j].Id;
            }

            orgUsers = await efOrgUserRepos[i].CreateMany(orgUsers);

            var selectionReadOnlyList = new List<CollectionAccessSelection>();
            orgUsers.ForEach(ou => selectionReadOnlyList.Add(new CollectionAccessSelection() { Id = ou.Id }));

            await efCollectionRepos[i].UpdateUsersAsync(efCollection.Id, selectionReadOnlyList);
            efCollectionRepos[i].ClearChangeTracking();

            foreach (var ou in orgUsers)
            {
                var collectionUser = new CollectionUser()
                {
                    CollectionId = efCollection.Id,
                    OrganizationUserId = ou.Id
                };
            }

            cipher.UserId = null;
            var postEfCipher = await sut.CreateAsync(cipher);
            sut.ClearChangeTracking();

            var query = new UserBumpAccountRevisionDateByCipherIdQuery(cipher.Id, cipher.OrganizationId.Value);
            var modifiedUsers = await sut.Run(query).ToListAsync();
            Assert.True(modifiedUsers
                .All(u => u.AccountRevisionDate.ToShortDateString() ==
                    DateTime.UtcNow.ToShortDateString()));
        }
    }

    [CiSkippedTheory, EfUserCipherCustomize, BitAutoData]
    public async Task UserCipher_DeleteAsync_CipherIsDeleted(
        Cipher cipher,
        User user,
        Organization org,
        List<EfRepo.CipherRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos
            ) => await DeleteAsync_CipherIsDeleted(cipher, user, org, suts, efUserRepos, efOrgRepos);
    [CiSkippedTheory, EfOrganizationCipherCustomize, BitAutoData]
    public Task OrganizationCipher_DeleteAsync_CipherIsDeleted(
        Cipher cipher,
        User user,
        Organization org,
        List<EfRepo.CipherRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos
            )
    {
        DeleteAsync_CipherIsDeleted(cipher, user, org, suts, efUserRepos, efOrgRepos);
        return Task.CompletedTask;
    }

    private async Task DeleteAsync_CipherIsDeleted(
        Cipher cipher,
        User user,
        Organization org,
        List<EfRepo.CipherRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos
            )
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            efOrgRepos[i].ClearChangeTracking();
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            efUserRepos[i].ClearChangeTracking();

            if (cipher.OrganizationId.HasValue)
            {
                cipher.OrganizationId = postEfOrg.Id;
            }
            cipher.UserId = postEfUser.Id;

            await sut.CreateAsync(cipher);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(cipher);
            sut.ClearChangeTracking();

            var savedCipher = await sut.GetByIdAsync(cipher.Id);
            Assert.True(savedCipher == null);
        }
    }
}
