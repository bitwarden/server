using System.Text;
using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohortAssignments;
using Bit.Core;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationPlanMigrationCohortAssignmentsController))]
[SutProviderCustomize]
public class OrganizationPlanMigrationCohortAssignmentsControllerTests
{
    private static IFormFile CsvFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "cohorts.csv");
    }

    private static void EnableFlag(SutProvider<OrganizationPlanMigrationCohortAssignmentsController> sut) =>
        sut.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);

    private static void WithTempData(OrganizationPlanMigrationCohortAssignmentsController sut) =>
        sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

    [Theory, BitAutoData]
    public void Index_FlagDisabled_ReturnsNotFound(
        SutProvider<OrganizationPlanMigrationCohortAssignmentsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

        Assert.IsType<NotFoundResult>(sutProvider.Sut.Index());
    }

    [Theory, BitAutoData]
    public async Task Upload_ValidationErrors_ReturnsIndexViewWithErrors(
        SutProvider<OrganizationPlanMigrationCohortAssignmentsController> sutProvider)
    {
        EnableFlag(sutProvider);
        WithTempData(sutProvider.Sut);

        BillingCommandResult<CohortBulkAssignmentResult> commandResult = new CohortBulkAssignmentResult
        {
            Errors = [new CohortBulkAssignmentError(2, "Organization x does not exist.")],
        };
        sutProvider.GetDependency<IBulkSyncCohortAssignmentsCommand>()
            .Run(Arg.Any<IFormFile>())
            .Returns(commandResult);

        var model = new BulkAssignmentUploadModel { File = CsvFile("OrganizationId,CohortName\n") };
        var result = await sutProvider.Sut.Upload(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        var returnedModel = Assert.IsType<BulkAssignmentUploadModel>(view.Model);
        var error = Assert.Single(returnedModel.Errors);
        Assert.Equal(2, error.LineNumber);
    }

    [Theory, BitAutoData]
    public async Task Upload_Clean_CommitsAndReturnsResultView(
        SutProvider<OrganizationPlanMigrationCohortAssignmentsController> sutProvider)
    {
        EnableFlag(sutProvider);
        WithTempData(sutProvider.Sut);

        BillingCommandResult<CohortBulkAssignmentResult> commandResult = new CohortBulkAssignmentResult
        {
            Summary = new CohortBulkAssignmentSummary { Inserted = 5 },
        };
        sutProvider.GetDependency<IBulkSyncCohortAssignmentsCommand>()
            .Run(Arg.Any<IFormFile>())
            .Returns(commandResult);

        var model = new BulkAssignmentUploadModel { File = CsvFile("OrganizationId,CohortName\n") };
        var result = await sutProvider.Sut.Upload(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Result", view.ViewName);
        var resultModel = Assert.IsType<BulkAssignmentResultModel>(view.Model);
        Assert.Equal(5, resultModel.Summary.Inserted);
    }

    [Theory, BitAutoData]
    public async Task Upload_CommandUnhandled_ReturnsIndexViewWithError(
        SutProvider<OrganizationPlanMigrationCohortAssignmentsController> sutProvider)
    {
        EnableFlag(sutProvider);
        WithTempData(sutProvider.Sut);

        BillingCommandResult<CohortBulkAssignmentResult> commandResult = new Unhandled();
        sutProvider.GetDependency<IBulkSyncCohortAssignmentsCommand>()
            .Run(Arg.Any<IFormFile>())
            .Returns(commandResult);

        var model = new BulkAssignmentUploadModel { File = CsvFile("OrganizationId,CohortName\n") };
        var result = await sutProvider.Sut.Upload(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
    }
}
