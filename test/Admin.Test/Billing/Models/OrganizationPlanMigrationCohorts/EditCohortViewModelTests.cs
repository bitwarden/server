using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Admin.Test.Billing.Models.OrganizationPlanMigrationCohorts;

public class EditCohortViewModelTests
{
    [Theory, BitAutoData]
    public void From_NullMigrationPathId_MapsToChurnOnlyCohortType(
        OrganizationPlanMigrationCohort cohort,
        CohortFormModel formModel,
        CohortAssignmentState assignmentState)
    {
        cohort.MigrationPathId = null;

        var viewModel = EditCohortViewModel.From(cohort, formModel, assignmentState);

        Assert.IsType<CohortType.ChurnOnly>(viewModel.CohortType);
    }

    [Theory, BitAutoData]
    public void From_KnownMigrationPathId_MapsToMigrationCohortType(
        OrganizationPlanMigrationCohort cohort,
        CohortFormModel formModel,
        CohortAssignmentState assignmentState)
    {
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;

        var viewModel = EditCohortViewModel.From(cohort, formModel, assignmentState);

        Assert.IsType<CohortType.Migration>(viewModel.CohortType);
    }

    [Theory, BitAutoData]
    public void From_UnknownMigrationPathId_MapsToUnresolvedMigrationCohortType(
        OrganizationPlanMigrationCohort cohort,
        CohortFormModel formModel,
        CohortAssignmentState assignmentState)
    {
        cohort.MigrationPathId = (MigrationPathId)99;

        var viewModel = EditCohortViewModel.From(cohort, formModel, assignmentState);

        Assert.IsType<CohortType.UnresolvedMigration>(viewModel.CohortType);
    }

    [Theory, BitAutoData]
    public void From_RoundTripsIsActiveAssignmentStateAndFormModel(
        OrganizationPlanMigrationCohort cohort,
        CohortFormModel formModel,
        CohortAssignmentState assignmentState)
    {
        cohort.IsActive = true;

        var viewModel = EditCohortViewModel.From(cohort, formModel, assignmentState);

        Assert.True(viewModel.IsActive);
        Assert.Same(assignmentState, viewModel.AssignmentState);
        Assert.Same(formModel, viewModel.FormModel);
    }
}
