using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

/// <summary>
/// Describes a supported transition from a legacy organization plan to a current
/// organization plan.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Id"/> is persisted on
/// <see cref="Entities.OrganizationPlanMigrationCohort.MigrationPathId"/> and is
/// referenced downstream by Stripe coupon decisions, scheduler routing, and audit
/// history. The underlying byte value can never be renumbered or reused for a
/// different path; see <see cref="MigrationPathId"/> and its snapshot tests for the
/// immortality guard.
/// </para>
/// <para>
/// <see cref="SeatCountPolicy"/> defaults to <see cref="Enums.SeatCountPolicy.Preserve"/> so
/// existing scalable-to-scalable paths keep the source seat quantity. Packaged sources whose
/// seat total cannot be read from the source line items (e.g. Teams 2019) set
/// <see cref="Enums.SeatCountPolicy.ActualUsage"/>.
/// </para>
/// </remarks>
public sealed record MigrationPath(
    MigrationPathId Id,
    string Name,
    PlanType FromPlan,
    PlanType ToPlan,
    SeatCountPolicy SeatCountPolicy = SeatCountPolicy.Preserve);
