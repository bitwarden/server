using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Repositories;

public interface IOrganizationPlanMigrationCohortAssignmentRepository
    : IRepository<OrganizationPlanMigrationCohortAssignment, Guid>
{
    /// <summary>
    /// Returns the single assignment for the given organization, or <c>null</c> when the
    /// organization has not been assigned to any cohort. At most one assignment per
    /// organization is enforced by a UNIQUE constraint at the database layer.
    /// </summary>
    Task<OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Atomically claims the per-assignment churn-mitigation one-shot guard by stamping
    /// <see cref="OrganizationPlanMigrationCohortAssignment.ChurnDiscountAppliedDate"/> to
    /// <paramref name="now"/> only when it is currently <c>NULL</c>. Returns <c>true</c> when
    /// the row was updated (the caller won the race), <c>false</c> when the row was already
    /// claimed (the caller lost the race). The conditional WHERE clause is the only
    /// post-consumption defense for <c>once</c>-duration churn coupons -- after the coupon
    /// is consumed at the next invoice it falls off Stripe's subscription discounts, so the
    /// Stripe-side "coupon currently attached" check is insufficient on its own.
    /// </summary>
    Task<bool> TryClaimChurnDiscountAsync(Guid id, DateTime now);
}
