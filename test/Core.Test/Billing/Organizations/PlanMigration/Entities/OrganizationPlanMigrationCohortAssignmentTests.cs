using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Entities;

public class OrganizationPlanMigrationCohortAssignmentTests
{
    public static TheoryData<DateTime?, DateTime?, DateTime?, bool> IsLockedCases =>
        new()
        {
            { null, null, null, false },
            { DateTime.UtcNow, null, null, true },
            { null, DateTime.UtcNow, null, true },
            { null, null, DateTime.UtcNow, true },
            { DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true },
        };

    [Theory]
    [MemberData(nameof(IsLockedCases))]
    public void IsLocked_ReturnsTrue_WhenAnyLifecycleDateIsSet(
        DateTime? scheduledAt,
        DateTime? migratedAt,
        DateTime? churnDiscountAppliedAt,
        bool expected)
    {
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = Guid.NewGuid(),
            CohortId = Guid.NewGuid(),
            ScheduledDate = scheduledAt,
            MigratedDate = migratedAt,
            ChurnDiscountAppliedDate = churnDiscountAppliedAt,
        };

        Assert.Equal(expected, assignment.IsLocked());
    }
}
