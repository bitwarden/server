using Bit.Core.AdminConsole.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.Repositories.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using OrganizationRepository = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.OrganizationRepository;
using Policy = Bit.Core.Entities.Policy;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Repositories;

public class PolicyRepositoryTests
{
    [CiSkippedTheory, EfPolicyAutoData]
    public async void CreateAsync_Works_DataMatches(
        Policy policy,
        Organization organization,
        PolicyCompare equalityComparer,
        List<EfRepo.PolicyRepository> suts,
        List<OrganizationRepository> efOrganizationRepos,
        SqlRepo.PolicyRepository sqlPolicyRepo,
        Dapper.AdminConsole.Repositories.OrganizationRepository sqlOrganizationRepo
        )
    {
        var savedPolicys = new List<Policy>();
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
