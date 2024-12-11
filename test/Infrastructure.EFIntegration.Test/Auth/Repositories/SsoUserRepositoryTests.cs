using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.Auth.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Auth.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Auth.Repositories;

public class SsoUserRepositoryTests
{
    [CiSkippedTheory, EfSsoUserAutoData]
    public async Task CreateAsync_Works_DataMatches(
        SsoUser ssoUser,
        User user,
        Organization org,
        SsoUserCompare equalityComparer,
        List<EfRepo.SsoUserRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        List<EfRepo.UserRepository> efUserRepos,
        SqlAuthRepo.SsoUserRepository sqlSsoUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo,
        SqlRepo.UserRepository sqlUserRepo
    )
    {
        var createdSsoUsers = new List<SsoUser>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efUser = await efUserRepos[i].CreateAsync(user);
            var efOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoUser.UserId = efUser.Id;
            ssoUser.OrganizationId = efOrg.Id;
            var postEfSsoUser = await sut.CreateAsync(ssoUser);
            sut.ClearChangeTracking();

            var savedSsoUser = await sut.GetByIdAsync(ssoUser.Id);
            createdSsoUsers.Add(savedSsoUser);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlOrganization = await sqlOrgRepo.CreateAsync(org);

        ssoUser.UserId = sqlUser.Id;
        ssoUser.OrganizationId = sqlOrganization.Id;
        var sqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);

        createdSsoUsers.Add(await sqlSsoUserRepo.GetByIdAsync(sqlSsoUser.Id));

        var distinctSsoUsers = createdSsoUsers.Distinct(equalityComparer);
        Assert.True(!distinctSsoUsers.Skip(1).Any());
    }

    [CiSkippedTheory, EfSsoUserAutoData]
    public async Task ReplaceAsync_Works_DataMatches(
        SsoUser postSsoUser,
        SsoUser replaceSsoUser,
        Organization org,
        User user,
        SsoUserCompare equalityComparer,
        List<EfRepo.SsoUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlAuthRepo.SsoUserRepository sqlSsoUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo,
        SqlRepo.UserRepository sqlUserRepo
    )
    {
        var savedSsoUsers = new List<SsoUser>();
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var efUser = await efUserRepos[i].CreateAsync(user);
            var efOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            postSsoUser.UserId = efUser.Id;
            postSsoUser.OrganizationId = efOrg.Id;
            var postEfSsoUser = await sut.CreateAsync(postSsoUser);
            sut.ClearChangeTracking();

            replaceSsoUser.Id = postEfSsoUser.Id;
            replaceSsoUser.UserId = postEfSsoUser.UserId;
            replaceSsoUser.OrganizationId = postEfSsoUser.OrganizationId;
            await sut.ReplaceAsync(replaceSsoUser);
            sut.ClearChangeTracking();

            var replacedSsoUser = await sut.GetByIdAsync(replaceSsoUser.Id);
            savedSsoUsers.Add(replacedSsoUser);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlOrganization = await sqlOrgRepo.CreateAsync(org);

        postSsoUser.UserId = sqlUser.Id;
        postSsoUser.OrganizationId = sqlOrganization.Id;
        var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(postSsoUser);

        replaceSsoUser.Id = postSqlSsoUser.Id;
        replaceSsoUser.UserId = postSqlSsoUser.UserId;
        replaceSsoUser.OrganizationId = postSqlSsoUser.OrganizationId;
        await sqlSsoUserRepo.ReplaceAsync(replaceSsoUser);

        savedSsoUsers.Add(await sqlSsoUserRepo.GetByIdAsync(replaceSsoUser.Id));

        var distinctItems = savedSsoUsers.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfSsoUserAutoData]
    public async Task DeleteAsync_Works_DataMatches(
        SsoUser ssoUser,
        Organization org,
        User user,
        List<EfRepo.SsoUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlAuthRepo.SsoUserRepository sqlSsoUserRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo
    )
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfUser = await efUserRepos[i].CreateAsync(user);
            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoUser.UserId = savedEfUser.Id;
            ssoUser.OrganizationId = savedEfOrg.Id;
            var postEfSsoUser = await sut.CreateAsync(ssoUser);
            sut.ClearChangeTracking();

            var savedEfSsoUser = await sut.GetByIdAsync(postEfSsoUser.Id);
            Assert.True(savedEfSsoUser != null);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(savedEfSsoUser);
            savedEfSsoUser = await sut.GetByIdAsync(savedEfSsoUser.Id);
            Assert.True(savedEfSsoUser == null);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
        ssoUser.UserId = sqlUser.Id;
        ssoUser.OrganizationId = sqlOrganization.Id;

        var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);
        var savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
        Assert.True(savedSqlSsoUser != null);

        await sqlSsoUserRepo.DeleteAsync(savedSqlSsoUser);
        savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
        Assert.True(savedSqlSsoUser == null);
    }

    [CiSkippedTheory, EfSsoUserAutoData]
    public async Task DeleteAsync_UserIdOrganizationId_Works_DataMatches(
        SsoUser ssoUser,
        User user,
        Organization org,
        List<EfRepo.SsoUserRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrgRepos,
        SqlAuthRepo.SsoUserRepository sqlSsoUserRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo
    )
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfUser = await efUserRepos[i].CreateAsync(user);
            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoUser.UserId = savedEfUser.Id;
            ssoUser.OrganizationId = savedEfOrg.Id;
            var postEfSsoUser = await sut.CreateAsync(ssoUser);
            sut.ClearChangeTracking();

            var savedEfSsoUser = await sut.GetByIdAsync(postEfSsoUser.Id);
            Assert.True(savedEfSsoUser != null);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(savedEfSsoUser.UserId, savedEfSsoUser.OrganizationId);
            sut.ClearChangeTracking();

            savedEfSsoUser = await sut.GetByIdAsync(savedEfSsoUser.Id);
            Assert.True(savedEfSsoUser == null);
        }

        var sqlUser = await sqlUserRepo.CreateAsync(user);
        var sqlOrganization = await sqlOrgRepo.CreateAsync(org);
        ssoUser.UserId = sqlUser.Id;
        ssoUser.OrganizationId = sqlOrganization.Id;

        var postSqlSsoUser = await sqlSsoUserRepo.CreateAsync(ssoUser);
        var savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
        Assert.True(savedSqlSsoUser != null);

        await sqlSsoUserRepo.DeleteAsync(savedSqlSsoUser.UserId, savedSqlSsoUser.OrganizationId);
        savedSqlSsoUser = await sqlSsoUserRepo.GetByIdAsync(postSqlSsoUser.Id);
        Assert.True(savedSqlSsoUser == null);
    }
}
