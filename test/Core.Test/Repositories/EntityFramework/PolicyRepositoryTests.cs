using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
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
    }
}
