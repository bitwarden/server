using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Entities;

public class OrganizationPlanMigrationCohortTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    public void IsMigrationPathLocked_GatesOnNonPendingCount(int nonPendingCount, bool expected)
    {
        var cohort = new OrganizationPlanMigrationCohort();

        var locked = cohort.IsMigrationPathLocked(nonPendingCount);

        Assert.Equal(expected, locked);
    }
}
