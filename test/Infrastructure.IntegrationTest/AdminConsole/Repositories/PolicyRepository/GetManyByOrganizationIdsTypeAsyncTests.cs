using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.PolicyRepository;

public class GetManyByOrganizationIdsTypeAsyncTests
{
    [Theory, DatabaseData]
    public async Task GetManyByOrganizationIdsTypeAsync_ReturnsOnlyRequestedOrganizationsAndType(
        IPolicyRepository policyRepository,
        IOrganizationRepository organizationRepository)
    {
        // Arrange
        var organization1 = await organizationRepository.CreateTestOrganizationAsync();
        var organization2 = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();

        var requireSso1 = await CreatePolicyAsync(policyRepository, organization1, PolicyType.RequireSso, true);
        var requireSso2 = await CreatePolicyAsync(policyRepository, organization2, PolicyType.RequireSso, false);
        await CreatePolicyAsync(policyRepository, organization1, PolicyType.SingleOrg, true);
        await CreatePolicyAsync(policyRepository, otherOrganization, PolicyType.RequireSso, true);

        // Act
        var results = await policyRepository.GetManyByOrganizationIdsTypeAsync(
            [organization1.Id, organization2.Id], PolicyType.RequireSso);

        // Assert
        Assert.Equal(
            new[] { requireSso1.Id, requireSso2.Id }.Order(),
            results.Select(p => p.Id).Order());
    }

    private static Task<Policy> CreatePolicyAsync(
        IPolicyRepository policyRepository, Organization organization, PolicyType type, bool enabled)
        => policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = type,
            Enabled = enabled
        });
}
