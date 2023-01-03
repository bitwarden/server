using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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

    [CiSkippedTheory]
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, false)]      // Ordinary user
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Invited, true, false)]        // Invited user
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.Owner, false, OrganizationUserStatusType.Confirmed, true, false)]     // Owner
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.Admin, false, OrganizationUserStatusType.Confirmed, true, false)]     // Admin
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, true, OrganizationUserStatusType.Confirmed, true, false)]       // canManagePolicies
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, true)]       // Provider
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, false, false)]     // Policy disabled
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, false)]      // No policy of Type
    [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Invited, true, false)]        // User not minStatus
    public async void GetByUserIdWithPolicyDetailsAsync_Works_DataMatches(
        // Inline data
        OrganizationUserType userType,
        bool canManagePolicies,
        OrganizationUserStatusType orgUserStatus,
        bool policyEnabled,
        bool isProvider,

        // Auto data - models
        Policy policy,
        User user,
        Organization organization,
        OrganizationUser orgUser,
        Provider provider,
        ProviderOrganization providerOrganization,
        ProviderUser providerUser,
        OrganizationUserPolicyDetailsCompare equalityComparer,

        // Auto data - EF repos
        List<EfRepo.PolicyRepository> efPolicyRepository,
        List<EfRepo.UserRepository> efUserRepository,
        List<EfRepo.OrganizationRepository> efOrganizationRepository,
        List<EfRepo.OrganizationUserRepository> suts,
        List<EfRepo.ProviderRepository> efProviderRepository,
        List<EfRepo.ProviderOrganizationRepository> efProviderOrganizationRepository,
        List<EfRepo.ProviderUserRepository> efProviderUserRepository,

        // Auto data - SQL repos
        SqlRepo.PolicyRepository sqlPolicyRepo,
        SqlRepo.UserRepository sqlUserRepo,
        SqlRepo.OrganizationRepository sqlOrganizationRepo,
        SqlRepo.ProviderRepository sqlProviderRepo,
        SqlRepo.OrganizationUserRepository sqlOrganizationUserRepo,
        SqlRepo.ProviderOrganizationRepository sqlProviderOrganizationRepo,
        SqlRepo.ProviderUserRepository sqlProviderUserRepo
        )
    {
        // Combine EF and SQL repos into one list per type
        var policyRepos = efPolicyRepository.ToList<IPolicyRepository>();
        policyRepos.Add(sqlPolicyRepo);
        var userRepos = efUserRepository.ToList<IUserRepository>();
        userRepos.Add(sqlUserRepo);
        var orgRepos = efOrganizationRepository.ToList<IOrganizationRepository>();
        orgRepos.Add(sqlOrganizationRepo);
        var orgUserRepos = suts.ToList<IOrganizationUserRepository>();
        orgUserRepos.Add(sqlOrganizationUserRepo);
        var providerRepos = efProviderRepository.ToList<IProviderRepository>();
        providerRepos.Add(sqlProviderRepo);
        var providerOrgRepos = efProviderOrganizationRepository.ToList<IProviderOrganizationRepository>();
        providerOrgRepos.Add(sqlProviderOrganizationRepo);
        var providerUserRepos = efProviderUserRepository.ToList<IProviderUserRepository>();
        providerUserRepos.Add(sqlProviderUserRepo);

        // Arrange data
        var savedPolicyType = PolicyType.SingleOrg;

        orgUser.Type = userType;
        orgUser.Status = orgUserStatus;
        var permissionsData = new Permissions { ManagePolicies = canManagePolicies };
        orgUser.Permissions = JsonSerializer.Serialize(permissionsData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        policy.Enabled = policyEnabled;
        policy.Type = savedPolicyType;

        var results = new List<OrganizationUserPolicyDetails>();

        foreach (var policyRepo in policyRepos)
        {
            var i = policyRepos.IndexOf(policyRepo);

            // Seed database
            user.CreationDate = user.RevisionDate = DateTime.Now;
            var savedUser = await userRepos[i].CreateAsync(user);
            var savedOrg = await orgRepos[i].CreateAsync(organization);

            // Invited orgUsers are not associated with an account yet, so they are identified by Email not UserId
            if (orgUserStatus == OrganizationUserStatusType.Invited)
            {
                orgUser.Email = savedUser.Email;
                orgUser.UserId = null;
            }
            else
            {
                orgUser.UserId = savedUser.Id;
            }

            orgUser.OrganizationId = savedOrg.Id;
            await orgUserRepos[i].CreateAsync(orgUser);

            if (isProvider)
            {
                var savedProvider = await providerRepos[i].CreateAsync(provider);

                providerOrganization.OrganizationId = savedOrg.Id;
                providerOrganization.ProviderId = savedProvider.Id;
                await providerOrgRepos[i].CreateAsync(providerOrganization);

                providerUser.UserId = savedUser.Id;
                providerUser.ProviderId = savedProvider.Id;
                await providerUserRepos[i].CreateAsync(providerUser);
            }

            policy.OrganizationId = savedOrg.Id;
            await policyRepo.CreateAsync(policy);
            if (efPolicyRepository.Contains(policyRepo))
            {
                (policyRepo as EfRepo.BaseEntityFrameworkRepository).ClearChangeTracking();
            }

            // Act
            var result = await orgUserRepos[i].GetByUserIdWithPolicyDetailsAsync(savedUser.Id);
            results.Add(result.FirstOrDefault());
        }

        // Assert
        var distinctItems = results.Distinct(equalityComparer);

        Assert.True(results.All(r => r == null) ||
            !distinctItems.Skip(1).Any());
    }
}
