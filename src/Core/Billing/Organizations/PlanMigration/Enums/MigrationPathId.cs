namespace Bit.Core.Billing.Organizations.PlanMigration.Enums;

/// <summary>
/// Stable identifier for a supported <see cref="ValueObjects.MigrationPath"/>.
/// </summary>
/// <remarks>
/// <para>
/// Values are persisted on <c>OrganizationPlanMigrationCohort.MigrationPathId</c> and
/// referenced downstream by Stripe coupon decisions, scheduler routing, and audit
/// history. Once a cohort row cites a value, that value's meaning (the source plan
/// and target plan it represents) is fixed forever.
/// </para>
/// <para>
/// Append new entries; never renumber or remove an existing one. The
/// <c>MigrationPathIdsSnapshotTests</c> fixture guards this invariant.
/// </para>
/// </remarks>
public enum MigrationPathId : byte
{
    Enterprise2020AnnualToCurrent = 1,
    Enterprise2020MonthlyToCurrent = 2,
    Teams2020AnnualToCurrent = 3,
    Teams2020MonthlyToCurrent = 4,
    Enterprise2019AnnualToCurrent = 5,
    Enterprise2019MonthlyToCurrent = 6,
    TeamsStarterToCurrent = 7,
    TeamsStarter2023ToCurrent = 8,
    Teams2019AnnualToCurrent = 9,
    Teams2019MonthlyToCurrent = 10,
}
