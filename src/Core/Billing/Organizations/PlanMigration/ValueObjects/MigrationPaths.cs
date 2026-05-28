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

    public static IReadOnlyList<MigrationPath> All { get; } =
    [
        Enterprise2020AnnualToCurrent,
        Enterprise2020MonthlyToCurrent,
        Teams2020AnnualToCurrent,
        Teams2020MonthlyToCurrent,
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
