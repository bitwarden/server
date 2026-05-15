namespace Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// The registry of all supported <see cref="MigrationPath"/> values. The byte
/// <see cref="MigrationPath.Id"/> for each registered path is persisted in customer
/// records and must never be renumbered or removed. Add new paths by appending to
/// <see cref="All"/>; never modify an existing entry's <see cref="MigrationPath.Id"/>.
/// </summary>
public static class MigrationPaths
{
    public static readonly MigrationPath Enterprise2020AnnualToCurrent = new(
        Id: 1,
        Name: nameof(Enterprise2020AnnualToCurrent),
        FromPlan: "Enterprise (2020, Annual)",
        ToPlan: "Enterprise");

    public static readonly MigrationPath Enterprise2020MonthlyToCurrent = new(
        Id: 2,
        Name: nameof(Enterprise2020MonthlyToCurrent),
        FromPlan: "Enterprise (2020, Monthly)",
        ToPlan: "Enterprise");

    public static IReadOnlyList<MigrationPath> All { get; } =
    [
        Enterprise2020AnnualToCurrent,
        Enterprise2020MonthlyToCurrent,
    ];

    /// <summary>
    /// Looks up a <see cref="MigrationPath"/> by its persisted byte
    /// <see cref="MigrationPath.Id"/>. Returns <c>null</c> for unknown IDs so callers can
    /// decide how to handle data referencing a path that has not yet been registered
    /// (e.g., during a multi-stage rollout).
    /// </summary>
    public static MigrationPath? FromId(byte id) =>
        All.FirstOrDefault(path => path.Id == id);
}
