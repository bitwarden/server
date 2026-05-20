using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Billing.Organizations.PlanMigration.Entities;

/// <summary>
/// Records that a specific organization belongs to a specific
/// <see cref="OrganizationPlanMigrationCohort"/> and tracks the organization's progress
/// through the migration lifecycle (scheduled, migrated, churn-mitigated).
/// </summary>
/// <remarks>
/// At most one assignment row exists per organization, enforced by a UNIQUE constraint
/// on <see cref="OrganizationId"/>. Once an assignment is created, its
/// <see cref="OrganizationId"/> and <see cref="CohortId"/> are immutable -- changing the
/// cohort for an organization means deleting the existing row and creating a new one.
/// </remarks>
public class OrganizationPlanMigrationCohortAssignment : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CohortId { get; set; }

    /// <summary>
    /// The date the scheduler picked up this assignment for migration. Null until the
    /// scheduler claims the row.
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// The date the migration completed. Null until the migration runs successfully.
    /// </summary>
    public DateTime? MigratedAt { get; set; }

    /// <summary>
    /// The date a churn-mitigation discount was applied. Null when no mitigation has
    /// occurred.
    /// </summary>
    public DateTime? ChurnDiscountAppliedAt { get; set; }

    public DateTime CreatedAt { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public bool IsLocked() =>
        MigratedAt.HasValue || ScheduledAt.HasValue || ChurnDiscountAppliedAt.HasValue;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
