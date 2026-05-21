using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Repositories;

public interface IOrganizationPlanMigrationCohortRepository
    : IRepository<OrganizationPlanMigrationCohort, Guid>
{
    /// <summary>
    /// Returns every cohort -- active and inactive -- ordered by <see cref="OrganizationPlanMigrationCohort.Name"/>.
    /// Operators routinely pre-stage assignments against inactive cohorts before activation, so the listing must
    /// not filter on <see cref="OrganizationPlanMigrationCohort.IsActive"/>.
    /// </summary>
    Task<IReadOnlyList<OrganizationPlanMigrationCohort>> GetManyAsync();

    Task<IEnumerable<CohortListItem>> SearchWithCountsAsync(string? name, int skip, int take);

    Task<OrganizationPlanMigrationCohort?> GetByNameAsync(string name);
}
