using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// Snapshot tests for <see cref="MigrationPaths"/>. The byte
/// <see cref="MigrationPath.Id"/> for every registered path is persisted on
/// <c>OrganizationPlanMigrationCohort.MigrationPathId</c> and referenced by Stripe
/// coupon decisions, scheduler routing, and audit history. Once a customer record cites
/// byte <c>N</c>, that byte means a specific FromPlan -> ToPlan transition forever.
///
/// <para>
/// IF YOU NEED TO ADD A PATH: append a new <c>Assert.Equal</c> below and bump the
/// expected count in <see cref="MigrationPaths_All_CountIsExpected"/>.
/// </para>
///
/// <para>
/// IF YOU NEED TO RENUMBER OR REMOVE A PATH: stop. Renumbering reuses a byte for a
/// different transition and silently mis-routes anyone in a cohort that cited the old
/// meaning. Removing a path orphans existing rows. Neither is safe -- if a path needs
/// to be retired, mark it inactive in product code instead.
/// </para>
/// </summary>
public class MigrationPathIdsSnapshotTests
{
    [Fact]
    public void MigrationPath_Ids_AreImmutable()
    {
        // These byte values are persisted into customer records. They cannot be
        // renumbered. Adding a new path? Append a new assertion below; do not change
        // existing ones.
        Assert.Equal(1, MigrationPaths.Enterprise2020AnnualToCurrent.Id);
        Assert.Equal(2, MigrationPaths.Enterprise2020MonthlyToCurrent.Id);
    }

    [Fact]
    public void MigrationPaths_All_CountIsExpected()
    {
        // Guards against accidental removal. Increment when intentionally adding a
        // new path.
        Assert.Equal(2, MigrationPaths.All.Count);
    }

    [Fact]
    public void MigrationPaths_All_IdsAreUnique()
    {
        var ids = MigrationPaths.All.Select(path => path.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
