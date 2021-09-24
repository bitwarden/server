using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TableModel = Bit.Core.Models.Table;
using System.Linq;
using System.Collections.Generic;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Bit.Core.Models.Data;
using System.Text.Json;
using Bit.Core.Enums;
using System.Threading.Tasks;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class PolicyRepositoryTests
    {
        [CiSkippedTheory, EfPolicyAutoData]
        public async void CreateAsync_Works_DataMatches(
            TableModel.Policy policy,
            TableModel.Organization organization,
            PolicyCompare equalityComparer,
            List<EfRepo.PolicyRepository> suts,
            List<EfRepo.OrganizationRepository> efOrganizationRepos,
            SqlRepo.PolicyRepository sqlPolicyRepo,
            SqlRepo.OrganizationRepository sqlOrganizationRepo
            )
        {
            var savedPolicys = new List<TableModel.Policy>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                var efOrganization = await efOrganizationRepos[i].CreateAsync(organization);
                sut.ClearChangeTracking();

                policy.OrganizationId = efOrganization.Id;
                var postEfPolicy = await sut.CreateAsync(policy);
                sut.ClearChangeTracking();

                var savedPolicy = await sut.GetByIdAsync(postEfPolicy.Id);
                savedPolicys.Add(savedPolicy);
            }

            var sqlOrganization = await sqlOrganizationRepo.CreateAsync(organization);

            policy.OrganizationId = sqlOrganization.Id;
            var sqlPolicy = await sqlPolicyRepo.CreateAsync(policy);
            var savedSqlPolicy = await sqlPolicyRepo.GetByIdAsync(sqlPolicy.Id);
            savedPolicys.Add(savedSqlPolicy);

            var distinctItems = savedPolicys.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }

        [CiSkippedTheory]
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, true, false)]       // Ordinary user
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.Owner, false, OrganizationUserStatusType.Confirmed, true, true, false)]     // Owner
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.Admin, false, OrganizationUserStatusType.Confirmed, true, true, false)]     // Admin
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, true, OrganizationUserStatusType.Confirmed, true, true, false)]       // canManagePolicies
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, true, true)]       // Provider
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, false, true, false)]     // Policy disabled
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Confirmed, true, false, false)]     // No policy of Type
        [EfPolicyApplicableToUserInlineAutoData(OrganizationUserType.User, false, OrganizationUserStatusType.Invited, true, true, false)]        // User not minStatus
        public async void GetManyByTypeApplicableToUser_Works_DataMatches_Corre(
            // Inline data
            OrganizationUserType userType,
            bool canManagePolicies,
            OrganizationUserStatusType orgUserStatus,
            bool policyEnabled,
            bool policySameType,
            bool isProvider,

            // Auto data - models
            TableModel.Policy policy,
            TableModel.User user,
            TableModel.Organization organization,
            TableModel.OrganizationUser orgUser,
            TableModel.Provider.Provider provider,
            TableModel.Provider.ProviderOrganization providerOrganization,
            TableModel.Provider.ProviderUser providerUser,
            PolicyCompareIncludingOrganization equalityComparer,

            // Auto data - EF repos
            List<EfRepo.PolicyRepository> suts,
            List<EfRepo.UserRepository> efUserRepository,
            List<EfRepo.OrganizationRepository> efOrganizationRepository,
            List<EfRepo.OrganizationUserRepository> efOrganizationUserRepos,
            List<EfRepo.ProviderRepository> efProviderRepos,
            List<EfRepo.ProviderOrganizationRepository> efProviderOrganizationRepository,
            List<EfRepo.ProviderUserRepository> efProviderUserRepository,

            // Auto data - SQL repos
            SqlRepo.PolicyRepository sqlPolicyRepo,
            SqlRepo.UserRepository sqlUserRepo,
            SqlRepo.ProviderRepository sqlProviderRepo,
            SqlRepo.OrganizationRepository sqlOrganizationRepo,
            SqlRepo.OrganizationUserRepository sqlOrganizationUserRepo,
            SqlRepo.ProviderOrganizationRepository sqlProviderOrganizationRepo,
            SqlRepo.ProviderUserRepository sqlProviderUserRepo
            )
        {
            var savedPolicyType = PolicyType.SingleOrg;
            var queriedPolicyType = policySameType ? savedPolicyType : PolicyType.DisableSend;

            // Arrange data
            orgUser.Type = userType;
            orgUser.Status = orgUserStatus;
            var permissionsData = new Permissions { ManagePolicies = canManagePolicies };
            orgUser.Permissions = JsonSerializer.Serialize(permissionsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            policy.OrganizationId = organization.Id;
            policy.Enabled = policyEnabled;
            policy.Type = savedPolicyType;

            var results = new List<TableModel.Policy>();

            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);

                // Seed EF databases
                var savedUser = await efUserRepository[i].CreateAsync(user);
                var savedOrg = await efOrganizationRepository[i].CreateAsync(organization);

                orgUser.UserId = savedUser.Id;
                orgUser.OrganizationId = savedOrg.Id;
                await efOrganizationUserRepos[i].CreateAsync(orgUser);

                if (isProvider)
                {
                    var savedProvider = await efProviderRepos[i].CreateAsync(provider);

                    providerOrganization.OrganizationId = savedOrg.Id;
                    providerOrganization.ProviderId = savedProvider.Id;
                    await efProviderOrganizationRepository[i].CreateAsync(providerOrganization);

                    providerUser.UserId = savedUser.Id;
                    providerUser.ProviderId = savedProvider.Id;
                    await efProviderUserRepository[i].CreateAsync(providerUser);
                }

                policy.OrganizationId = savedOrg.Id;
                await sut.CreateAsync(policy);
                sut.ClearChangeTracking();

                // Act on EF databases
                var result = await sut.GetManyByTypeApplicableToUserIdAsync(savedUser.Id, queriedPolicyType, OrganizationUserStatusType.Accepted);
                results.Add(result.FirstOrDefault());
            }

            // Seed SQL database
            var sqlUser = await sqlUserRepo.CreateAsync(user);
            var sqlOrg = await sqlOrganizationRepo.CreateAsync(organization);

            orgUser.UserId = sqlUser.Id;
            orgUser.OrganizationId = sqlOrg.Id;
            await sqlOrganizationUserRepo.CreateAsync(orgUser);

            if (isProvider)
            {
                var sqlProvider = await sqlProviderRepo.CreateAsync(provider);

                providerOrganization.OrganizationId = sqlOrg.Id;
                providerOrganization.ProviderId = sqlProvider.Id;
                await sqlProviderOrganizationRepo.CreateAsync(providerOrganization);

                providerUser.UserId = sqlUser.Id;
                providerUser.ProviderId = sqlProvider.Id;
                await sqlProviderUserRepo.CreateAsync(providerUser);
            }

            policy.OrganizationId = sqlOrg.Id;
            await sqlPolicyRepo.CreateAsync(policy);

            // Act on SQL database
            var sqlResult = await sqlPolicyRepo.GetManyByTypeApplicableToUserIdAsync(sqlUser.Id, queriedPolicyType, OrganizationUserStatusType.Accepted);
            results.Add(sqlResult.FirstOrDefault());

            // Assert
            var distinctItems = results.Distinct(equalityComparer);
            
            Assert.True(
                results.All(r => r == null) ||
                !distinctItems.Skip(1).Any()
            );
        }
    }
}
