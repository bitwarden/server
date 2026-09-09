using Bit.Core.Billing.Organizations.PlanMigration.Models;

namespace Bit.Admin.Billing.Models.OrganizationPlanMigrationCohortAssignments;

public class BulkAssignmentResultModel
{
    public required CohortBulkAssignmentSummary Summary { get; init; }
}
