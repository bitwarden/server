using Bit.Core.Billing.Organizations.PlanMigration.Entities;

namespace Bit.Core.Billing.Organizations.PlanMigration.Models;

public class CohortListItem
{
    public OrganizationPlanMigrationCohort Cohort { get; set; } = null!;
}
