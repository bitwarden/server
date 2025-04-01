﻿using AutoMapper;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using Organization = Bit.Core.AdminConsole.Entities.Organization;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class OrganizationRepositoryTests
{
    [Fact]
    public void ValidateOrganizationMappings_ReturnsSuccess()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<OrganizationMapperProfile>());
        config.AssertConfigurationIsValid();
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task CreateAsync_Works_DataMatches(
        Organization organization,
        SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer,
        List<EfRepo.OrganizationRepository> suts)
    {
        var savedOrganizations = new List<Organization>();
        foreach (var sut in suts)
        {
            var postEfOrganization = await sut.CreateAsync(organization);
            sut.ClearChangeTracking();

            var savedOrganization = await sut.GetByIdAsync(organization.Id);
            savedOrganizations.Add(savedOrganization);
        }

        var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);
        savedOrganizations.Add(await sqlOrganizationRepo.GetByIdAsync(sqlOrganization.Id));

        var distinctItems = savedOrganizations.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task ReplaceAsync_Works_DataMatches(Organization postOrganization,
        Organization replaceOrganization, SqlRepo.OrganizationRepository sqlOrganizationRepo,
        OrganizationCompare equalityComparer, List<EfRepo.OrganizationRepository> suts)
    {
        var savedOrganizations = new List<Organization>();
        foreach (var sut in suts)
        {
            var postEfOrganization = await sut.CreateAsync(postOrganization);
            sut.ClearChangeTracking();

            replaceOrganization.Id = postEfOrganization.Id;
            await sut.ReplaceAsync(replaceOrganization);
            sut.ClearChangeTracking();

            var replacedOrganization = await sut.GetByIdAsync(replaceOrganization.Id);
            savedOrganizations.Add(replacedOrganization);
        }

        var postSqlOrganization = await sqlOrganizationRepo.CreateAsync(postOrganization);
        replaceOrganization.Id = postSqlOrganization.Id;
        await sqlOrganizationRepo.ReplaceAsync(replaceOrganization);
        savedOrganizations.Add(await sqlOrganizationRepo.GetByIdAsync(replaceOrganization.Id));

        var distinctItems = savedOrganizations.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task DeleteAsync_Works_DataMatches(Organization organization,
        SqlRepo.OrganizationRepository sqlOrganizationRepo, List<EfRepo.OrganizationRepository> suts)
    {
        foreach (var sut in suts)
        {
            var postEfOrganization = await sut.CreateAsync(organization);
            sut.ClearChangeTracking();

            var savedEfOrganization = await sut.GetByIdAsync(postEfOrganization.Id);
            sut.ClearChangeTracking();
            Assert.True(savedEfOrganization != null);

            await sut.DeleteAsync(savedEfOrganization);
            sut.ClearChangeTracking();

            savedEfOrganization = await sut.GetByIdAsync(savedEfOrganization.Id);
            Assert.True(savedEfOrganization == null);
        }

        var postSqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);
        var savedSqlOrganization = await sqlOrganizationRepo.GetByIdAsync(postSqlOrganization.Id);
        Assert.True(savedSqlOrganization != null);

        await sqlOrganizationRepo.DeleteAsync(postSqlOrganization);
        savedSqlOrganization = await sqlOrganizationRepo.GetByIdAsync(postSqlOrganization.Id);
        Assert.True(savedSqlOrganization == null);
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task GetByIdentifierAsync_Works_DataMatches(Organization organization,
        SqlRepo.OrganizationRepository sqlOrganizationRepo, OrganizationCompare equalityComparer,
        List<EfRepo.OrganizationRepository> suts)
    {
        var returnedOrgs = new List<Organization>();
        foreach (var sut in suts)
        {
            var postEfOrg = await sut.CreateAsync(organization);
            sut.ClearChangeTracking();

            var returnedOrg = await sut.GetByIdentifierAsync(postEfOrg.Identifier.ToUpperInvariant());
            returnedOrgs.Add(returnedOrg);
        }

        var postSqlOrg = await sqlOrganizationRepo.CreateAsync(organization);
        returnedOrgs.Add(await sqlOrganizationRepo.GetByIdentifierAsync(postSqlOrg.Identifier.ToUpperInvariant()));

        var distinctItems = returnedOrgs.Distinct(equalityComparer);
        Assert.True(!distinctItems.Skip(1).Any());
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task GetManyByEnabledAsync_Works_DataMatches(Organization organization,
        SqlRepo.OrganizationRepository sqlOrganizationRepo, List<EfRepo.OrganizationRepository> suts)
    {
        var returnedOrgs = new List<Organization>();
        foreach (var sut in suts)
        {
            var postEfOrg = await sut.CreateAsync(organization);
            sut.ClearChangeTracking();

            var efReturnedOrgs = await sut.GetManyByEnabledAsync();
            returnedOrgs.Concat(efReturnedOrgs);
        }

        var postSqlOrg = await sqlOrganizationRepo.CreateAsync(organization);
        returnedOrgs.Concat(await sqlOrganizationRepo.GetManyByEnabledAsync());

        Assert.True(returnedOrgs.All(o => o.Enabled));
    }

    // testing data matches here would require manipulating all organization abilities in the db
    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task GetManyAbilitiesAsync_Works(SqlRepo.OrganizationRepository sqlOrganizationRepo, List<EfRepo.OrganizationRepository> suts)
    {
        var list = new List<OrganizationAbility>();
        foreach (var sut in suts)
        {
            list.Concat(await sut.GetManyAbilitiesAsync());
        }

        list.Concat(await sqlOrganizationRepo.GetManyAbilitiesAsync());
        Assert.True(list.All(x => x.GetType() == typeof(OrganizationAbility)));
    }

    [CiSkippedTheory, EfOrganizationUserAutoData]
    public async Task SearchUnassignedAsync_Works(OrganizationUser orgUser, User user, Organization org,
        List<EfRepo.OrganizationUserRepository> efOrgUserRepos, List<EfRepo.OrganizationRepository> efOrgRepos, List<EfRepo.UserRepository> efUserRepos,
        SqlRepo.OrganizationUserRepository sqlOrgUserRepo, SqlRepo.OrganizationRepository sqlOrgRepo, SqlRepo.UserRepository sqlUserRepo)
    {
        orgUser.Type = OrganizationUserType.Owner;
        org.PlanType = PlanType.EnterpriseAnnually;

        var efList = new List<Organization>();
        foreach (var efOrgUserRepo in efOrgUserRepos)
        {
            var i = efOrgUserRepos.IndexOf(efOrgUserRepo);
            var postEfUser = await efUserRepos[i].CreateAsync(user);
            var postEfOrg = await efOrgRepos[i].CreateAsync(org);
            efOrgUserRepo.ClearChangeTracking();

            orgUser.UserId = postEfUser.Id;
            orgUser.OrganizationId = postEfOrg.Id;
            await efOrgUserRepo.CreateAsync(orgUser);
            efOrgUserRepo.ClearChangeTracking();

            efList.AddRange(await efOrgRepos[i].SearchUnassignedToProviderAsync(org.Name, user.Email, 0, 10));
        }

        var postSqlUser = await sqlUserRepo.CreateAsync(user);
        var postSqlOrg = await sqlOrgRepo.CreateAsync(org);

        orgUser.UserId = postSqlUser.Id;
        orgUser.OrganizationId = postSqlOrg.Id;
        await sqlOrgUserRepo.CreateAsync(orgUser);
        var sqlResult = await sqlOrgRepo.SearchUnassignedToProviderAsync(org.Name, user.Email, 0, 10);

        Assert.Equal(efOrgRepos.Count, efList.Count);
        Assert.True(efList.All(o => o.Name == org.Name));
        Assert.Single(sqlResult);
        Assert.True(sqlResult.All(o => o.Name == org.Name));
    }

    [CiSkippedTheory, EfOrganizationAutoData]
    public async Task GetManyByIdsAsync_Works_DataMatches(List<Organization> organizations,
        SqlRepo.OrganizationRepository sqlOrganizationRepo,
        List<EfRepo.OrganizationRepository> suts)
    {
        var returnedOrgs = new List<Organization>();

        foreach (var sut in suts)
        {
            _ = await sut.CreateMany(organizations);
            sut.ClearChangeTracking();

            var efReturnedOrgs = await sut.GetManyByIdsAsync(organizations.Select(o => o.Id).ToList());
            returnedOrgs.AddRange(efReturnedOrgs);
        }

        foreach (var organization in organizations)
        {
            var postSqlOrg = await sqlOrganizationRepo.CreateAsync(organization);
            returnedOrgs.Add(await sqlOrganizationRepo.GetByIdAsync(postSqlOrg.Id));
        }

        var orgIds = organizations.Select(o => o.Id).ToList();
        var distinctReturnedOrgIds = returnedOrgs.Select(o => o.Id).Distinct().ToList();

        Assert.Equal(orgIds.Count, distinctReturnedOrgIds.Count);
        Assert.Equivalent(orgIds, distinctReturnedOrgIds);

        // clean up
        foreach (var organization in organizations)
        {
            await sqlOrganizationRepo.DeleteAsync(organization);
            foreach (var sut in suts)
            {
                await sut.DeleteAsync(organization);
            }
        }
    }
}
