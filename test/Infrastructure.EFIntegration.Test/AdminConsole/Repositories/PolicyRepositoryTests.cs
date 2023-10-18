using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AdminConsole.AutoFixture;
using Bit.Infrastructure.EFIntegration.Test.AdminConsole.Repositories.EqualityComparers;
using Xunit;
using OrganizationRepository = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.OrganizationRepository;
using Policy = Bit.Core.Entities.Policy;
using PolicyRepository = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.PolicyRepository;

namespace Bit.Infrastructure.EFIntegration.Test.AdminConsole.Repositories;

public class PolicyRepositoryTests
{
    [CiSkippedTheory, EfPolicyAutoData]
    public async void CreateAsync_Works_DataMatches(
        Policy policy,
        Organization organization,
        PolicyCompare equalityComparer,
        List<PolicyRepository> suts,
        List<OrganizationRepository> efOrganizationRepos,
        Dapper.AdminConsole.Repositories.PolicyRepository sqlPolicyRepo,
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
