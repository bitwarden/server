using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models.Cohorts;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(CohortsController))]
[SutProviderCustomize]
public class CohortsControllerTests
{
    [Theory, BitAutoData]
    public async Task Index_ReturnsViewWithEmptyCohortsPagedModel(
        SutProvider<CohortsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .SearchWithCountsAsync(null, 0, 25)
            .Returns(Array.Empty<CohortListItem>());

        var result = await sutProvider.Sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CohortsPagedModel>(view.Model);
        Assert.Empty(model.Items);
    }

    [Theory, BitAutoData]
    public async Task Index_PassesRepoItemsThroughToModel(
        List<CohortListItem> repoItems,
        SutProvider<CohortsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .SearchWithCountsAsync(null, 0, 25)
            .Returns(repoItems);

        var result = await sutProvider.Sut.Index();

        var model = Assert.IsType<CohortsPagedModel>(((ViewResult)result).Model);
        Assert.Equal(repoItems.Count, model.Items.Count);
    }

    [Theory, BitAutoData]
    public async Task Index_RowCarriesCohortName(
        CohortListItem repoItem,
        SutProvider<CohortsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .SearchWithCountsAsync(null, 0, 25)
            .Returns(new[] { repoItem });

        var result = await sutProvider.Sut.Index();

        var model = Assert.IsType<CohortsPagedModel>(((ViewResult)result).Model);
        var row = Assert.Single(model.Items);
        Assert.Equal(repoItem.Cohort.Name, row.Name);
    }

    [Theory, BitAutoData]
    public async Task Index_WithNameFilter_PassesNameToRepository(
        SutProvider<CohortsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .SearchWithCountsAsync("alpha", 0, 25)
            .Returns(Array.Empty<CohortListItem>());

        await sutProvider.Sut.Index(name: "alpha");

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .SearchWithCountsAsync("alpha", 0, 25);
    }

    [Theory, BitAutoData]
    public async Task Index_WithPage2_CalculatesSkipCorrectly(
        SutProvider<CohortsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .SearchWithCountsAsync(null, 25, 25)
            .Returns(Array.Empty<CohortListItem>());

        await sutProvider.Sut.Index(page: 2);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .SearchWithCountsAsync(null, 25, 25);
    }

    [Theory, BitAutoData]
    public async Task Create_Post_ValidMigrationCohort_CreatesInactiveCohortAndRedirects(
        CohortFormModel model,
        SutProvider<CohortsController> sutProvider)
    {
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationPlanMigrationCohort>(c =>
                c.Name == model.Name
                && c.MigrationPathId == MigrationPathId.Enterprise2020AnnualToCurrent
                && c.IsActive == false));
    }

    [Theory, BitAutoData]
    public async Task Create_Post_DuplicateName_AddsModelErrorOnName(
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<CohortsController> sutProvider)
    {
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name)
            .Returns(existing);

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.True(sutProvider.Sut.ModelState.ContainsKey(nameof(model.Name)));
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationPlanMigrationCohort>());
    }
}
