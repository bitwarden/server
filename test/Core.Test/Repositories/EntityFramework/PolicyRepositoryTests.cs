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

        [CiSkippedTheory, EfPolicyAutoData]
        public async void GetManyByTypeApplicableToUser_Works_DataMatches(
            TableModel.Policy policy,
            TableModel.OrganizationUser orgUser,
            TableModel.Provider.ProviderOrganization providerOrganization,
            TableModel.Provider.ProviderUser providerUser,
            PolicyCompare equalityComparer,
            List<EfRepo.PolicyRepository> suts,
            List<EfRepo.OrganizationUserRepository> efOrganizationUserRepos,
            List<EfRepo.ProviderOrganizationRepository> efProviderOrganizationRepository,
            List<EfRepo.ProviderUserRepository> efProviderUserRepository,
            SqlRepo.PolicyRepository sqlPolicyRepo,
            SqlRepo.OrganizationUserRepository sqlOrganizationUserRepo,
            SqlRepo.ProviderOrganizationRepository sqlProviderOrganizationRepo,
            SqlRepo.ProviderUserRepository sqlProviderUserRepo
            )
        {
            // Arrange

            // TODO: paramaterize these values
            // Expected result: policy applies
            orgUser.Type = Enums.OrganizationUserType.User;
            orgUser.Permissions = null;
            orgUser.Status = Enums.OrganizationUserStatusType.Confirmed;

            policy.OrganizationId = orgUser.OrganizationId;
            policy.Enabled = true;

            providerUser.UserId = orgUser.UserId;
            providerUser.ProviderId = providerOrganization.ProviderId;

            var results = new List<TableModel.Policy>();

            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);
                var orgUserRepo = efOrganizationUserRepos[i];
                var providerOrgRepo = efProviderOrganizationRepository[i];
                var providerUserRepo = efProviderUserRepository[i];

                // Seed database
                await orgUserRepo.CreateAsync(orgUser);
                await providerOrgRepo.CreateAsync(providerOrganization);
                await providerUserRepo.CreateAsync(providerUser);
                await sut.CreateAsync(policy);
                sut.ClearChangeTracking();

                // Act
                var result = await sut.GetManyByTypeApplicableToUserIdAsync(orgUser.UserId.Value, policy.Type, Enums.OrganizationUserStatusType.Accepted);
                results.Add(result.FirstOrDefault());
            }

            // Seed Sql database
            await sqlOrganizationUserRepo.CreateAsync(orgUser);
            await sqlProviderOrganizationRepo.CreateAsync(providerOrganization);
            await sqlProviderUserRepo.CreateAsync(providerUser);
            await sqlPolicyRepo.CreateAsync(policy);

            var sqlResult = await sqlPolicyRepo.GetManyByTypeApplicableToUserIdAsync(orgUser.UserId.Value, policy.Type, Enums.OrganizationUserStatusType.Accepted);
            results.Add(sqlResult.FirstOrDefault());

            var distinctItems = results.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }
    }
}
