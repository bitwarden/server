using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.PlanMigration;

public class CohortBulkAssignmentRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetPlanTypesByOrganizationIdsAsync_ReturnsIdAndPlanType_ForExistingOrgsOnly(
        IOrganizationRepository organizationRepository)
    {
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Enterprise (Annually) 2020",
            PlanType = PlanType.EnterpriseAnnually2020,
        });

        var missingId = Guid.NewGuid();

        var results = await organizationRepository.GetPlanTypesByOrganizationIdsAsync([org.Id, missingId]);

        var row = Assert.Single(results);
        Assert.Equal(org.Id, row.OrganizationId);
        Assert.Equal(PlanType.EnterpriseAnnually2020, row.PlanType);
    }
}
