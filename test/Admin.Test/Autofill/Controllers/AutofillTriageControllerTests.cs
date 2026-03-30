using Bit.Admin.Controllers;
using Bit.Admin.Models;
using Bit.Core.Autofill.Entities;
using Bit.Core.Autofill.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Admin.Test.Autofill.Controllers;

[ControllerCustomize(typeof(AutofillTriageController))]
[SutProviderCustomize]
public class AutofillTriageControllerTests
{
    [Theory, BitAutoData]
    public async Task Index_ReturnsViewWithReports(
        List<AutofillTriageReport> reports,
        SutProvider<AutofillTriageController> sutProvider)
    {
        sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .GetActiveAsync(0, 25)
            .Returns(reports);

        var result = await sutProvider.Sut.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AutofillTriageModel>(viewResult.Model);
        Assert.Equal(reports.Count, model.Items.Count);
        Assert.Equal(1, model.Page);
        Assert.Equal(25, model.Count);
    }

    [Theory, BitAutoData]
    public async Task Index_WithPagination_CalculatesCorrectSkip(
        List<AutofillTriageReport> reports,
        SutProvider<AutofillTriageController> sutProvider)
    {
        sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .GetActiveAsync(50, 25)
            .Returns(reports);

        await sutProvider.Sut.Index(page: 3, count: 25);

        await sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .Received(1)
            .GetActiveAsync(50, 25);
    }

    [Theory, BitAutoData]
    public async Task Details_WhenReportExists_ReturnsView(
        AutofillTriageReport report,
        SutProvider<AutofillTriageController> sutProvider)
    {
        sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .GetByIdAsync(report.Id)
            .Returns(report);

        var result = await sutProvider.Sut.Details(report.Id);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(report, viewResult.Model);
    }

    [Theory, BitAutoData]
    public async Task Details_WhenReportNotFound_ReturnsNotFound(
        Guid id,
        SutProvider<AutofillTriageController> sutProvider)
    {
        sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .GetByIdAsync(id)
            .Returns((AutofillTriageReport?)null);

        var result = await sutProvider.Sut.Details(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Archive_CallsArchiveAsyncAndRedirectsToIndex(
        Guid id,
        SutProvider<AutofillTriageController> sutProvider)
    {
        var result = await sutProvider.Sut.Archive(id);

        await sutProvider.GetDependency<IAutofillTriageReportRepository>()
            .Received(1)
            .ArchiveAsync(id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AutofillTriageController.Index), redirect.ActionName);
    }
}
