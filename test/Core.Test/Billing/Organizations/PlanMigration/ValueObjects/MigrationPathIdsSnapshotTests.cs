using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// Snapshot tests for <see cref="MigrationPathId"/> and <see cref="MigrationPaths"/>.
/// The byte value of every registered path is persisted on
/// <c>OrganizationPlanMigrationCohort.MigrationPathId</c> and referenced by Stripe
/// coupon decisions, scheduler routing, and audit history. Once a customer record
/// cites byte <c>N</c>, that byte means a specific FromPlan -> ToPlan transition
/// forever.
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
    public void MigrationPathId_ByteValues_AreImmutable()
    {
        // These byte values are persisted into customer records. They cannot be
        // renumbered. Adding a new path? Append a new assertion below; do not change
        // existing ones.
        Assert.Equal((byte)1, (byte)MigrationPathId.Enterprise2020AnnualToCurrent);
        Assert.Equal((byte)2, (byte)MigrationPathId.Enterprise2020MonthlyToCurrent);
        Assert.Equal((byte)3, (byte)MigrationPathId.Teams2020AnnualToCurrent);
        Assert.Equal((byte)4, (byte)MigrationPathId.Teams2020MonthlyToCurrent);
        Assert.Equal((byte)5, (byte)MigrationPathId.Enterprise2019AnnualToCurrent);
        Assert.Equal((byte)6, (byte)MigrationPathId.Enterprise2019MonthlyToCurrent);
        Assert.Equal((byte)7, (byte)MigrationPathId.TeamsStarterToCurrent);
        Assert.Equal((byte)8, (byte)MigrationPathId.TeamsStarter2023ToCurrent);
        Assert.Equal((byte)9, (byte)MigrationPathId.Teams2019AnnualToCurrent);
        Assert.Equal((byte)10, (byte)MigrationPathId.Teams2019MonthlyToCurrent);
    }

    [Fact]
    public void MigrationPaths_RegistryEntries_PointAtMatchingIds()
    {
        // Sanity check that the registry's MigrationPath value object exposes the
        // expected MigrationPathId. Catches accidental copy/paste mistakes when
        // adding a new path.
        Assert.Equal(MigrationPathId.Enterprise2020AnnualToCurrent,
            MigrationPaths.Enterprise2020AnnualToCurrent.Id);
        Assert.Equal(MigrationPathId.Enterprise2020MonthlyToCurrent,
            MigrationPaths.Enterprise2020MonthlyToCurrent.Id);
        Assert.Equal(MigrationPathId.Teams2020AnnualToCurrent,
            MigrationPaths.Teams2020AnnualToCurrent.Id);
        Assert.Equal(MigrationPathId.Teams2020MonthlyToCurrent,
            MigrationPaths.Teams2020MonthlyToCurrent.Id);
        Assert.Equal(MigrationPathId.Enterprise2019AnnualToCurrent,
            MigrationPaths.Enterprise2019AnnualToCurrent.Id);
        Assert.Equal(MigrationPathId.Enterprise2019MonthlyToCurrent,
            MigrationPaths.Enterprise2019MonthlyToCurrent.Id);
        Assert.Equal(MigrationPathId.TeamsStarterToCurrent,
            MigrationPaths.TeamsStarterToCurrent.Id);
        Assert.Equal(MigrationPathId.TeamsStarter2023ToCurrent,
            MigrationPaths.TeamsStarter2023ToCurrent.Id);
        Assert.Equal(MigrationPathId.Teams2019AnnualToCurrent,
            MigrationPaths.Teams2019AnnualToCurrent.Id);
        Assert.Equal(MigrationPathId.Teams2019MonthlyToCurrent,
            MigrationPaths.Teams2019MonthlyToCurrent.Id);
    }

    [Fact]
    public void MigrationPaths_RegistryEntries_HaveImmutableFromAndToPlans()
    {
        // Each registered byte means a specific FromPlan -> ToPlan transition forever.
        // Locking byte values (above) without locking the FromPlan/ToPlan they map to
        // would let a refactor silently mis-route existing cohort rows. Adding a new
        // path? Append a new pair of assertions below; do not change existing ones.
        Assert.Equal(PlanType.EnterpriseAnnually2020,
            MigrationPaths.Enterprise2020AnnualToCurrent.FromPlan);
        Assert.Equal(PlanType.EnterpriseAnnually,
            MigrationPaths.Enterprise2020AnnualToCurrent.ToPlan);
        Assert.Equal(PlanType.EnterpriseMonthly2020,
            MigrationPaths.Enterprise2020MonthlyToCurrent.FromPlan);
        Assert.Equal(PlanType.EnterpriseMonthly,
            MigrationPaths.Enterprise2020MonthlyToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsAnnually2020,
            MigrationPaths.Teams2020AnnualToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsAnnually,
            MigrationPaths.Teams2020AnnualToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsMonthly2020,
            MigrationPaths.Teams2020MonthlyToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsMonthly,
            MigrationPaths.Teams2020MonthlyToCurrent.ToPlan);
        Assert.Equal(PlanType.EnterpriseAnnually2019,
            MigrationPaths.Enterprise2019AnnualToCurrent.FromPlan);
        Assert.Equal(PlanType.EnterpriseAnnually,
            MigrationPaths.Enterprise2019AnnualToCurrent.ToPlan);
        Assert.Equal(PlanType.EnterpriseMonthly2019,
            MigrationPaths.Enterprise2019MonthlyToCurrent.FromPlan);
        Assert.Equal(PlanType.EnterpriseMonthly,
            MigrationPaths.Enterprise2019MonthlyToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsStarter,
            MigrationPaths.TeamsStarterToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsMonthly,
            MigrationPaths.TeamsStarterToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsStarter2023,
            MigrationPaths.TeamsStarter2023ToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsMonthly,
            MigrationPaths.TeamsStarter2023ToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsAnnually2019,
            MigrationPaths.Teams2019AnnualToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsAnnually,
            MigrationPaths.Teams2019AnnualToCurrent.ToPlan);
        Assert.Equal(PlanType.TeamsMonthly2019,
            MigrationPaths.Teams2019MonthlyToCurrent.FromPlan);
        Assert.Equal(PlanType.TeamsMonthly,
            MigrationPaths.Teams2019MonthlyToCurrent.ToPlan);
    }

    [Fact]
    public void MigrationPaths_All_CountIsExpected()
    {
        // Guards against accidental removal. Increment when intentionally adding a
        // new path.
        Assert.Equal(10, MigrationPaths.All.Count);
    }

    [Fact]
    public void MigrationPaths_SeatCountPolicy_IsActualUsageForTeams2019AndPreserveOtherwise()
    {
        // Teams 2019 is a packaged base + per-seat-overage plan migrating to a pure per-seat
        // plan; its Phase 2 seat quantity is resolved from actual usage, not preserved from the
        // source line items. Every other path preserves the source seat quantity.
        Assert.Equal(SeatCountPolicy.ActualUsage,
            MigrationPaths.Teams2019AnnualToCurrent.SeatCountPolicy);
        Assert.Equal(SeatCountPolicy.ActualUsage,
            MigrationPaths.Teams2019MonthlyToCurrent.SeatCountPolicy);
        Assert.Equal(SeatCountPolicy.Preserve,
            MigrationPaths.Teams2020MonthlyToCurrent.SeatCountPolicy);
        Assert.Equal(SeatCountPolicy.Preserve,
            MigrationPaths.Enterprise2019AnnualToCurrent.SeatCountPolicy);
    }

    [Fact]
    public void MigrationPaths_All_IdsAreUnique()
    {
        var ids = MigrationPaths.All.Select(path => path.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
