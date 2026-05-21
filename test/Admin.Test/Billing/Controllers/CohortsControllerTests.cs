using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models.Cohorts;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;

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

    [Theory, BitAutoData]
    public async Task Create_Post_StripeProactiveResourceMissing_AddsErrorOnProactiveField(
        CohortFormModel model,
        SutProvider<CohortsController> sutProvider)
    {
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = "BAD-CODE";
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync("BAD-CODE", Arg.Any<CouponGetOptions?>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "resource_missing" } });

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.True(sutProvider.Sut.ModelState.ContainsKey(nameof(model.ProactiveDiscountCouponCode)));
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<OrganizationPlanMigrationCohort>());
    }

    [Theory, BitAutoData]
    public async Task Create_Post_StripeGenericFailure_AddsGenericErrorAndRejects(
        CohortFormModel model,
        SutProvider<CohortsController> sutProvider)
    {
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = "FOO";
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync("FOO", Arg.Any<CouponGetOptions?>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "rate_limit" } });

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        var fieldErrors = sutProvider.Sut.ModelState[nameof(model.ProactiveDiscountCouponCode)]!.Errors;
        Assert.Contains(fieldErrors,
            e => e.ErrorMessage.Contains("An error occurred while fetching the coupon from Stripe."));
    }

    [Theory, BitAutoData]
    public async Task Create_Post_BothCouponsInvalid_FlagsBothFields(
        CohortFormModel model,
        SutProvider<CohortsController> sutProvider)
    {
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = "BAD-P";
        model.ChurnDiscountCouponCode = "BAD-C";

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);
        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions?>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "resource_missing" } });

        var result = await sutProvider.Sut.Create(model);

        Assert.True(sutProvider.Sut.ModelState.ContainsKey(nameof(model.ProactiveDiscountCouponCode)));
        Assert.True(sutProvider.Sut.ModelState.ContainsKey(nameof(model.ChurnDiscountCouponCode)));
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_ExistingId_PrefillsForm(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<CohortsController> sutProvider)
    {
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);

        var result = await sutProvider.Sut.Edit(cohort.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CohortFormModel>(view.Model);
        Assert.Equal(cohort.Id, model.Id);
        Assert.Equal(cohort.Name, model.Name);
        Assert.Equal("1", model.MigrationPathSelection);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_HappyPath_UpdatesCohortAndBumpsRevisionDate(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<CohortsController> sutProvider)
    {
        existing.Id = id;
        existing.RevisionDate = DateTime.UtcNow.AddDays(-1);
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;
        var before = existing.RevisionDate;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohort>(c =>
                c.Id == id
                && c.Name == model.Name
                && c.MigrationPathId == MigrationPathId.Enterprise2020AnnualToCurrent
                && c.RevisionDate > before));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_SameNameSameCohort_AllowsUpdate(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<CohortsController> sutProvider)
    {
        existing.Id = id;
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns(existing);

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1).ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohort>());
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_InvalidModelState_SetsCohortTypeViewData(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<CohortsController> sutProvider)
    {
        existing.Id = id;
        existing.MigrationPathId = (MigrationPathId)99;
        model.Id = id;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);

        sutProvider.Sut.ModelState.AddModelError("force", "trigger invalid state");

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<ViewResult>(result);
        var cohortType = sutProvider.Sut.ViewData["CohortType"];
        Assert.IsType<CohortType.UnresolvedMigration>(cohortType);
    }
}
