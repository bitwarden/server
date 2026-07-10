using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Organizations.PlanMigration.Entities;

/// <summary>
/// A group of organizations that share a migration path from a legacy plan to a current plan.
/// Cohorts are managed by Bitwarden staff (typically via CSV import) and consumed by the
/// scheduler and churn-mitigation pipeline to drive plan migrations.
/// </summary>
public class OrganizationPlanMigrationCohort : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>
    /// A human-readable, globally unique cohort identifier. Used as the join key when
    /// importing assignments from CSV.
    /// </summary>
    [MaxLength(255)] public string Name { get; set; } = null!;

    /// <summary>
    /// Identifies which <see cref="ValueObjects.MigrationPath"/> this cohort follows.
    /// A non-null value is a Migration cohort; null is a Churn-only cohort.
    /// </summary>
    /// <remarks>
    /// Byte values are immortal once persisted; see <see cref="MigrationPathId"/>
    /// and its snapshot tests.
    /// </remarks>
    public MigrationPathId? MigrationPathId { get; set; }

    /// <summary>
    /// Optional Stripe coupon applied proactively when a cohort member is migrated to the
    /// current plan.
    /// </summary>
    [MaxLength(64)] public string? ProactiveDiscountCouponCode { get; set; }

    /// <summary>
    /// Optional Stripe coupon applied during the churn-mitigation flow if a cohort member
    /// initiates cancellation.
    /// </summary>
    [MaxLength(64)] public string? ChurnDiscountCouponCode { get; set; }

    /// <summary>
    /// When false, no automated migration actions are taken for assignments in this cohort.
    /// </summary>
    public bool IsActive { get; set; }

    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
