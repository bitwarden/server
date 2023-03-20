using Bit.Core.Auth.Entities;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.Dapper.Auth.Repositories;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlAuthRepo = Bit.Infrastructure.Dapper.Auth.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class SsoConfigRepositoryTests
{
    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void CreateAsync_Works_DataMatches(SsoConfig ssoConfig, Organization org,
        SsoConfigCompare equalityComparer, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlAuthRepo.SsoConfigRepository sqlSsoConfigRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var savedSsoConfigs = new List<SsoConfig>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoConfig.OrganizationId = savedEfOrg.Id;
            var postEfSsoConfig = await sut.CreateAsync(ssoConfig);
            sut.ClearChangeTracking();

            var savedEfSsoConfig = await sut.GetByIdAsync(ssoConfig.Id);
            Assert.True(savedEfSsoConfig != null);
            savedSsoConfigs.Add(savedEfSsoConfig);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
        ssoConfig.OrganizationId = sqlOrganization.Id;

        var sqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);
        var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(sqlSsoConfig.Id);
        Assert.True(savedSqlSsoConfig != null);
        savedSsoConfigs.Add(savedSqlSsoConfig);

        var distinctItems = savedSsoConfigs.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void ReplaceAsync_Works_DataMatches(SsoConfig postSsoConfig, SsoConfig replaceSsoConfig,
        Organization org, SsoConfigCompare equalityComparer, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlAuthRepo.SsoConfigRepository sqlSsoConfigRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        var savedSsoConfigs = new List<SsoConfig>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            postSsoConfig.OrganizationId = replaceSsoConfig.OrganizationId = savedEfOrg.Id;
            var postEfSsoConfig = await sut.CreateAsync(postSsoConfig);
            sut.ClearChangeTracking();

            replaceSsoConfig.Id = postEfSsoConfig.Id;
            savedSsoConfigs.Add(postEfSsoConfig);
            await sut.ReplaceAsync(replaceSsoConfig);
            sut.ClearChangeTracking();

            var replacedSsoConfig = await sut.GetByIdAsync(replaceSsoConfig.Id);
            Assert.True(replacedSsoConfig != null);
            savedSsoConfigs.Add(replacedSsoConfig);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
        postSsoConfig.OrganizationId = sqlOrganization.Id;

        var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(postSsoConfig);
        replaceSsoConfig.Id = postSqlSsoConfig.Id;
        savedSsoConfigs.Add(postSqlSsoConfig);

        await sqlSsoConfigRepo.ReplaceAsync(replaceSsoConfig);
        var replacedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(replaceSsoConfig.Id);
        Assert.True(replacedSqlSsoConfig != null);
        savedSsoConfigs.Add(replacedSqlSsoConfig);

        var distinctItems = savedSsoConfigs.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(2).Any());
    }

    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void DeleteAsync_Works_DataMatches(SsoConfig ssoConfig, Organization org, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlAuthRepo.SsoConfigRepository sqlSsoConfigRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo)
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoConfig.OrganizationId = savedEfOrg.Id;
            var postEfSsoConfig = await sut.CreateAsync(ssoConfig);
            sut.ClearChangeTracking();

            var savedEfSsoConfig = await sut.GetByIdAsync(postEfSsoConfig.Id);
            Assert.True(savedEfSsoConfig != null);
            sut.ClearChangeTracking();

            await sut.DeleteAsync(savedEfSsoConfig);
            var deletedEfSsoConfig = await sut.GetByIdAsync(savedEfSsoConfig.Id);
            Assert.True(deletedEfSsoConfig == null);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(org);
        ssoConfig.OrganizationId = sqlOrganization.Id;

        var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);
        var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(postSqlSsoConfig.Id);
        Assert.True(savedSqlSsoConfig != null);

        await sqlSsoConfigRepo.DeleteAsync(savedSqlSsoConfig);
        savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdAsync(postSqlSsoConfig.Id);
        Assert.True(savedSqlSsoConfig == null);
    }

    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void GetByOrganizationIdAsync_Works_DataMatches(SsoConfig ssoConfig, Organization org,
        SsoConfigCompare equalityComparer, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlAuthRepo.SsoConfigRepository sqlSsoConfigRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo)
    {
        var returnedList = new List<SsoConfig>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoConfig.OrganizationId = savedEfOrg.Id;
            await sut.CreateAsync(ssoConfig);
            sut.ClearChangeTracking();

            var savedEfUser = await sut.GetByOrganizationIdAsync(savedEfOrg.Id);
            Assert.True(savedEfUser != null);
            returnedList.Add(savedEfUser);
        }

        var savedSqlOrg = await sqlOrgRepo.CreateAsync(org);
        ssoConfig.OrganizationId = savedSqlOrg.Id;

        var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);

        var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByOrganizationIdAsync(ssoConfig.OrganizationId);
        Assert.True(savedSqlSsoConfig != null);
        returnedList.Add(savedSqlSsoConfig);

        var distinctItems = returnedList.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void GetByIdentifierAsync_Works_DataMatches(SsoConfig ssoConfig, Organization org,
        SsoConfigCompare equalityComparer, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos, SqlAuthRepo.SsoConfigRepository sqlSsoConfigRepo,
        SqlRepo.OrganizationRepository sqlOrgRepo)
    {
        var returnedList = new List<SsoConfig>();

        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoConfig.OrganizationId = savedEfOrg.Id;
            await sut.CreateAsync(ssoConfig);
            sut.ClearChangeTracking();

            var savedEfSsoConfig = await sut.GetByIdentifierAsync(org.Identifier);
            Assert.True(savedEfSsoConfig != null);
            returnedList.Add(savedEfSsoConfig);
        }

        var savedSqlOrg = await sqlOrgRepo.CreateAsync(org);
        ssoConfig.OrganizationId = savedSqlOrg.Id;

        var postSqlSsoConfig = await sqlSsoConfigRepo.CreateAsync(ssoConfig);

        var savedSqlSsoConfig = await sqlSsoConfigRepo.GetByIdentifierAsync(org.Identifier);
        Assert.True(savedSqlSsoConfig != null);
        returnedList.Add(savedSqlSsoConfig);

        var distinctItems = returnedList.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    // Testing that data matches here would involve manipulating all SsoConfig records in the db
    [CiSkippedTheory, EfSsoConfigAutoData]
    public async void GetManyByRevisionNotBeforeDate_Works(SsoConfig ssoConfig, DateTime notBeforeDate,
        Organization org, List<EfRepo.SsoConfigRepository> suts,
        List<EfRepo.OrganizationRepository> efOrgRepos)
    {
        foreach (var sut in suts)
        {
            var i = suts.IndexOf(sut);

            var savedEfOrg = await efOrgRepos[i].CreateAsync(org);
            sut.ClearChangeTracking();

            ssoConfig.OrganizationId = savedEfOrg.Id;
            await sut.CreateAsync(ssoConfig);
            sut.ClearChangeTracking();

            var returnedEfSsoConfigs = await sut.GetManyByRevisionNotBeforeDate(notBeforeDate);
            Assert.True(returnedEfSsoConfigs.All(sc => sc.RevisionDate >= notBeforeDate));
        }
    }
}
