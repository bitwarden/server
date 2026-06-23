using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// The registry of all supported <see cref="MigrationPath"/> values. The
/// <see cref="MigrationPathId"/> assigned to each registered path is persisted in
/// customer records and must never be renumbered or removed. Add new paths by
/// appending to <see cref="All"/>; never modify an existing entry's
/// <see cref="MigrationPath.Id"/>.
/// </summary>
public static class MigrationPaths
{
    public static readonly MigrationPath Enterprise2020AnnualToCurrent = new(
        Id: MigrationPathId.Enterprise2020AnnualToCurrent,
        Name: nameof(Enterprise2020AnnualToCurrent),
        FromPlan: PlanType.EnterpriseAnnually2020,
        ToPlan: PlanType.EnterpriseAnnually);

    public static readonly MigrationPath Enterprise2020MonthlyToCurrent = new(
        Id: MigrationPathId.Enterprise2020MonthlyToCurrent,
        Name: nameof(Enterprise2020MonthlyToCurrent),
        FromPlan: PlanType.EnterpriseMonthly2020,
        ToPlan: PlanType.EnterpriseMonthly);

    public static readonly MigrationPath Teams2020AnnualToCurrent = new(
        Id: MigrationPathId.Teams2020AnnualToCurrent,
        Name: nameof(Teams2020AnnualToCurrent),
        FromPlan: PlanType.TeamsAnnually2020,
        ToPlan: PlanType.TeamsAnnually);

    public static readonly MigrationPath Teams2020MonthlyToCurrent = new(
        Id: MigrationPathId.Teams2020MonthlyToCurrent,
        Name: nameof(Teams2020MonthlyToCurrent),
        FromPlan: PlanType.TeamsMonthly2020,
        ToPlan: PlanType.TeamsMonthly);

    public static readonly MigrationPath Enterprise2019AnnualToCurrent = new(
        Id: MigrationPathId.Enterprise2019AnnualToCurrent,
        Name: nameof(Enterprise2019AnnualToCurrent),
        FromPlan: PlanType.EnterpriseAnnually2019,
        ToPlan: PlanType.EnterpriseAnnually);

    public static readonly MigrationPath Enterprise2019MonthlyToCurrent = new(
        Id: MigrationPathId.Enterprise2019MonthlyToCurrent,
        Name: nameof(Enterprise2019MonthlyToCurrent),
        FromPlan: PlanType.EnterpriseMonthly2019,
        ToPlan: PlanType.EnterpriseMonthly);

    public static readonly MigrationPath TeamsStarterToCurrent = new(
        Id: MigrationPathId.TeamsStarterToCurrent,
        Name: nameof(TeamsStarterToCurrent),
        FromPlan: PlanType.TeamsStarter,
        ToPlan: PlanType.TeamsMonthly);

    public static readonly MigrationPath TeamsStarter2023ToCurrent = new(
        Id: MigrationPathId.TeamsStarter2023ToCurrent,
        Name: nameof(TeamsStarter2023ToCurrent),
        FromPlan: PlanType.TeamsStarter2023,
        ToPlan: PlanType.TeamsMonthly);

    // Teams 2019 is a Packaged base + seat-overage plan migrating to a Scalable plan, so its Phase 2
    // seat quantity is resolved from actual usage rather than preserved from the source line items.
    public static readonly MigrationPath Teams2019AnnualToCurrent = new(
        Id: MigrationPathId.Teams2019AnnualToCurrent,
        Name: nameof(Teams2019AnnualToCurrent),
        FromPlan: PlanType.TeamsAnnually2019,
        ToPlan: PlanType.TeamsAnnually,
        SeatCountPolicy: SeatCountPolicy.ActualUsage);

    public static readonly MigrationPath Teams2019MonthlyToCurrent = new(
        Id: MigrationPathId.Teams2019MonthlyToCurrent,
        Name: nameof(Teams2019MonthlyToCurrent),
        FromPlan: PlanType.TeamsMonthly2019,
        ToPlan: PlanType.TeamsMonthly,
        SeatCountPolicy: SeatCountPolicy.ActualUsage);

    public static IReadOnlyList<MigrationPath> All { get; } =
    [
        Enterprise2020AnnualToCurrent,
        Enterprise2020MonthlyToCurrent,
        Teams2020AnnualToCurrent,
        Teams2020MonthlyToCurrent,
        Enterprise2019AnnualToCurrent,
        Enterprise2019MonthlyToCurrent,
        TeamsStarterToCurrent,
        TeamsStarter2023ToCurrent,
        Teams2019AnnualToCurrent,
        Teams2019MonthlyToCurrent,
    ];

    /// <summary>
    /// Looks up a <see cref="MigrationPath"/> by its persisted
    /// <see cref="MigrationPathId"/>. Returns <c>null</c> for unknown IDs so callers
    /// can decide how to handle data referencing a path that has not yet been
    /// registered (e.g., during a multi-stage rollout).
    /// </summary>
    public static MigrationPath? FromId(MigrationPathId id) =>
        All.FirstOrDefault(path => path.Id == id);
}
