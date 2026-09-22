using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfAdminConsoleRepo = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using OrganizationUser = Bit.Core.Entities.OrganizationUser;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class OrganizationUserRepositoryTests
{
    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async Task CreateAsync_Works_DataMatches(OrganizationUser orgUser, User user, Organization org,
        OrganizationUserCompare equalityComparer, List<EfAdminConsoleRepo.OrganizationUserRepository> suts,
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
    public async Task ReplaceAsync_Works_DataMatches(
        OrganizationUser postOrgUser,
        OrganizationUser replaceOrgUser,
        User user,
        Organization org,
        OrganizationUserCompare equalityComparer,
        List<EfAdminConsoleRepo.OrganizationUserRepository> suts,
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
    public async Task DeleteAsync_Works_DataMatches(OrganizationUser orgUser, User user, Organization org, List<EfAdminConsoleRepo.OrganizationUserRepository> suts,
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

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async Task DeleteManyAsync_WithSsoUser_SsoUserIsDeleted(OrganizationUser orgUser, SsoUser ssoUser,
        User user, Organization org, List<EfAdminConsoleRepo.OrganizationUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos, List<EfRepo.OrganizationRepository> efOrgRepos,
        List<EfRepo.SsoUserRepository> efSsoUserRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo, SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo, SqlAuthRepo.SsoUserRepository sqlSsoUserRepo)
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

            ssoUser.UserId = postEfUser.Id;
            ssoUser.OrganizationId = postEfOrg.Id;
            await efSsoUserRepos[i].CreateAsync(ssoUser);
            efSsoUserRepos[i].ClearChangeTracking();

            await sut.DeleteManyAsync(new[] { postEfOrgUser.Id });
            sut.ClearChangeTracking();
            efSsoUserRepos[i].ClearChangeTracking();

            var savedEfOrgUser = await sut.GetByIdAsync(postEfOrgUser.Id);
            Assert.True(savedEfOrgUser == null);

            var savedEfSsoUser = await efSsoUserRepos[i].GetByUserIdOrganizationIdAsync(postEfOrg.Id, postEfUser.Id);
            Assert.True(savedEfSsoUser == null);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrg = await sqlOrgRepo.CreateAsync(org);

        orgUser.UserId = postSqlUser.Id;
        orgUser.OrganizationId = postSqlOrg.Id;
        var postSqlOrgUser = await sqlOrgUserRepo.CreateAsync(orgUser);

        ssoUser.UserId = postSqlUser.Id;
        ssoUser.OrganizationId = postSqlOrg.Id;
        await sqlSsoUserRepo.CreateAsync(ssoUser);

        await sqlOrgUserRepo.DeleteManyAsync(new[] { postSqlOrgUser.Id });

        var savedSqlOrgUser = await sqlOrgUserRepo.GetByIdAsync(postSqlOrgUser.Id);
        Assert.True(savedSqlOrgUser == null);

        var savedSqlSsoUser = await sqlSsoUserRepo.GetByUserIdOrganizationIdAsync(postSqlOrg.Id, postSqlUser.Id);
        Assert.True(savedSqlSsoUser == null);
    }

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async Task DeleteManyAsync_WithSsoUserInAnotherOrganization_OnlyDeletesRemovedOrganizationsSsoUser(
        OrganizationUser orgUserA, OrganizationUser orgUserB, SsoUser ssoUserA, SsoUser ssoUserB,
        User user, Organization orgA, Organization orgB, List<EfAdminConsoleRepo.OrganizationUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos, List<EfRepo.OrganizationRepository> efOrgRepos,
        List<EfRepo.SsoUserRepository> efSsoUserRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo, SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo, SqlAuthRepo.SsoUserRepository sqlSsoUserRepo)
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrgA = await efOrgRepos[i].CreateAsync(orgA);
            var postEfOrgB = await efOrgRepos[i].CreateAsync(orgB);
            sut.ClearChangeTracking();

            orgUserA.UserId = postEfUser.Id;
            orgUserA.OrganizationId = postEfOrgA.Id;
            var postEfOrgUserA = await sut.CreateAsync(orgUserA);
            sut.ClearChangeTracking();

            orgUserB.UserId = postEfUser.Id;
            orgUserB.OrganizationId = postEfOrgB.Id;
            await sut.CreateAsync(orgUserB);
            sut.ClearChangeTracking();

            ssoUserA.UserId = postEfUser.Id;
            ssoUserA.OrganizationId = postEfOrgA.Id;
            await efSsoUserRepos[i].CreateAsync(ssoUserA);
            efSsoUserRepos[i].ClearChangeTracking();

            ssoUserB.UserId = postEfUser.Id;
            ssoUserB.OrganizationId = postEfOrgB.Id;
            await efSsoUserRepos[i].CreateAsync(ssoUserB);
            efSsoUserRepos[i].ClearChangeTracking();

            await sut.DeleteManyAsync(new[] { postEfOrgUserA.Id });
            sut.ClearChangeTracking();
            efSsoUserRepos[i].ClearChangeTracking();

            var savedEfSsoUserA = await efSsoUserRepos[i].GetByUserIdOrganizationIdAsync(postEfOrgA.Id, postEfUser.Id);
            Assert.True(savedEfSsoUserA == null);

            var savedEfSsoUserB = await efSsoUserRepos[i].GetByUserIdOrganizationIdAsync(postEfOrgB.Id, postEfUser.Id);
            Assert.True(savedEfSsoUserB != null);
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrgA = await sqlOrgRepo.CreateAsync(orgA);
        var postSqlOrgB = await sqlOrgRepo.CreateAsync(orgB);

        orgUserA.UserId = postSqlUser.Id;
        orgUserA.OrganizationId = postSqlOrgA.Id;
        var postSqlOrgUserA = await sqlOrgUserRepo.CreateAsync(orgUserA);

        orgUserB.UserId = postSqlUser.Id;
        orgUserB.OrganizationId = postSqlOrgB.Id;
        await sqlOrgUserRepo.CreateAsync(orgUserB);

        ssoUserA.UserId = postSqlUser.Id;
        ssoUserA.OrganizationId = postSqlOrgA.Id;
        await sqlSsoUserRepo.CreateAsync(ssoUserA);

        ssoUserB.UserId = postSqlUser.Id;
        ssoUserB.OrganizationId = postSqlOrgB.Id;
        await sqlSsoUserRepo.CreateAsync(ssoUserB);

        await sqlOrgUserRepo.DeleteManyAsync(new[] { postSqlOrgUserA.Id });

        var savedSqlSsoUserA = await sqlSsoUserRepo.GetByUserIdOrganizationIdAsync(postSqlOrgA.Id, postSqlUser.Id);
        Assert.True(savedSqlSsoUserA == null);

        var savedSqlSsoUserB = await sqlSsoUserRepo.GetByUserIdOrganizationIdAsync(postSqlOrgB.Id, postSqlUser.Id);
        Assert.True(savedSqlSsoUserB != null);
    }
}
