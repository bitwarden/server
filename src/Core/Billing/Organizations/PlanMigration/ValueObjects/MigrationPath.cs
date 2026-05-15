namespace Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// Describes a supported transition from a legacy organization plan to a current
/// organization plan.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Id"/> byte value is persisted on
/// <see cref="Entities.OrganizationPlanMigrationCohort.MigrationPathId"/> and is referenced
/// downstream by Stripe coupon decisions, scheduler routing, and audit history. Once a
/// cohort has been assigned a byte value, that value can never be renumbered or reused
/// for a different path. See <see cref="MigrationPaths"/> for the registry and its
/// snapshot tests for the immortality guard.
/// </para>
/// </remarks>
public sealed record MigrationPath(byte Id, string Name, string FromPlan, string ToPlan);
