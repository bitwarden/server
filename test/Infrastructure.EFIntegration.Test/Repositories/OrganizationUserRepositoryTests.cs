using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using OrganizationUser = Bit.Core.Entities.OrganizationUser;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class OrganizationUserRepositoryTests
{
    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async void CreateAsync_Works_DataMatches(OrganizationUser orgUser, User user, Organization org,
        OrganizationUserCompare equalityComparer, List<EfRepo.OrganizationUserRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, List<EfRepo.UserRepository> efUserRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo, SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo)
    {
        var savedOrgUsers = new List<OrganizationUser>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            orgUser.UserId = postEfUser.Id;
            orgUser.OrganizationId = postEfOrg.Id;
            var postEfOrgUser = await sut.CreateAsync(orgUser);
            sut.ClearChangeTracking();

            var savedOrgUser = await sut.GetByIdAsync(postEfOrgUser.Id);
            savedOrgUsers.Add(savedOrgUser);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrg = await sqlOrgRepo.CreateAsync(org);

        orgUser.UserId = postSqlUser.Id;
        orgUser.OrganizationId = postSqlOrg.Id;
        var sqlOrgUser = await sqlOrgUserRepo.CreateAsync(orgUser);

        var savedSqlOrgUser = await sqlOrgUserRepo.GetByIdAsync(sqlOrgUser.Id);
        savedOrgUsers.Add(savedSqlOrgUser);

        var distinctItems = savedOrgUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async void ReplaceAsync_Works_DataMatches(
        OrganizationUser postOrgUser,
        OrganizationUser replaceOrgUser,
        User user,
        Organization org,
        OrganizationUserCompare equalityComparer,
        List<EfRepo.OrganizationUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo
        )
    {
        var savedOrgUsers = new List<OrganizationUser>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            postOrgUser.UserId = replaceOrgUser.UserId = postEfUser.Id;
            postOrgUser.OrganizationId = replaceOrgUser.OrganizationId = postEfOrg.Id;
            var postEfOrgUser = await sut.CreateAsync(postOrgUser);
            sut.ClearChangeTracking();

            replaceOrgUser.Id = postOrgUser.Id;
            await sut.ReplaceAsync(replaceOrgUser);
            sut.ClearChangeTracking();

            var replacedOrganizationUser = await sut.GetByIdAsync(replaceOrgUser.Id);
            savedOrgUsers.Add(replacedOrganizationUser);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrg = await sqlOrgRepo.CreateAsync(org);

        postOrgUser.UserId = replaceOrgUser.UserId = postSqlUser.Id;
        postOrgUser.OrganizationId = replaceOrgUser.OrganizationId = postSqlOrg.Id;
        var postSqlOrgUser = await sqlOrgUserRepo.CreateAsync(postOrgUser);

        replaceOrgUser.Id = postSqlOrgUser.Id;
        await sqlOrgUserRepo.ReplaceAsync(replaceOrgUser);

        var replacedSqlUser = await sqlOrgUserRepo.GetByIdAsync(replaceOrgUser.Id);

        var distinctItems = savedOrgUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async void DeleteAsync_Works_DataMatches(OrganizationUser orgUser, User user, Organization org, List<EfRepo.OrganizationUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos, List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo, SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo)
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            orgUser.UserId = postEfUser.Id;
            orgUser.OrganizationId = postEfOrg.Id;
            var postEfOrgUser = await sut.CreateAsync(orgUser);
            sut.ClearChangeTracking();

            var savedEfOrgUser = await sut.GetByIdAsync(postEfOrgUser.Id);
            Assert.True(savedEfOrgUser != null);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(savedEfOrgUser);
            sut.ClearChangeTracking();

            savedEfOrgUser = await sut.GetByIdAsync(savedEfOrgUser.Id);
            Assert.True(savedEfOrgUser == null);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrg = await sqlOrgRepo.CreateAsync(org);

        orgUser.UserId = postSqlUser.Id;
        orgUser.OrganizationId = postSqlOrg.Id;
        var postSqlOrgUser = await sqlOrgUserRepo.CreateAsync(orgUser);

        var savedSqlOrgUser = await sqlOrgUserRepo.GetByIdAsync(postSqlOrgUser.Id);
        Assert.True(savedSqlOrgUser != null);

        await sqlOrgUserRepo.DeleteAsync(postSqlOrgUser);
        savedSqlOrgUser = await sqlOrgUserRepo.GetByIdAsync(postSqlOrgUser.Id);
        Assert.True(savedSqlOrgUser == null);
    }
}
