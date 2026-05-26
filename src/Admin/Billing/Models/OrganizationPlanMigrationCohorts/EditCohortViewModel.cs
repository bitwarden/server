using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;

namespace Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;

public class EditCohortViewModel
{
    public required CohortFormModel FormModel { get; init; }
    public required CohortType CohortType { get; init; }
    public required bool IsActive { get; init; }
    public required CohortAssignmentState AssignmentState { get; init; }

    public static EditCohortViewModel From(
        OrganizationPlanMigrationCohort cohort,
        CohortFormModel formModel,
        CohortAssignmentState assignmentState) =>
        new()
        {
            FormModel = formModel,
            CohortType = CohortType.From(cohort.MigrationPathId),
            IsActive = cohort.IsActive,
            AssignmentState = assignmentState,
        };
}
