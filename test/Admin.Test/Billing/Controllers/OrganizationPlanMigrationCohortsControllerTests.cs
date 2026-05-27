using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;
using Bit.Core;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationPlanMigrationCohortsController))]
[SutProviderCustomize]
public class OrganizationPlanMigrationCohortsControllerTests
{
    [Theory, BitAutoData]
    public async Task Index_ReturnsViewWithEmptyCohortsPagedModel(
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
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
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        cohort.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        var result = await sutProvider.Sut.Edit(cohort.Id);

        var view = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<EditCohortViewModel>(view.Model);
        Assert.Equal(cohort.Id, viewModel.FormModel.Id);
        Assert.Equal(cohort.Name, viewModel.FormModel.Name);
        Assert.Equal("1", viewModel.FormModel.MigrationPathSelection);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_HappyPath_PersistsActiveCohortAndBumpsRevisionDate(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        existing.IsActive = false;
        existing.RevisionDate = DateTime.UtcNow.AddDays(-1);
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;
        model.IsActive = true;
        var before = existing.RevisionDate;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

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
                && c.IsActive
                && c.RevisionDate > before));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_HappyPath_PersistsInactiveWhenCheckboxUnchecked(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        existing.IsActive = true;
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;
        model.IsActive = false;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohort>(c => !c.IsActive));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_SameNameSameCohort_AllowsUpdate(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns(existing);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1).ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohort>());
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_InvalidModelState_SetsCohortTypeOnViewModel(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        existing.MigrationPathId = (MigrationPathId)99;
        model.Id = id;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        sutProvider.Sut.ModelState.AddModelError("force", "trigger invalid state");

        var result = await sutProvider.Sut.Edit(id, model);

        var view = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<EditCohortViewModel>(view.Model);
        Assert.IsType<CohortType.UnresolvedMigration>(viewModel.CohortType);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_DuplicateName_ReturnsViewWithEditViewModel(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        OrganizationPlanMigrationCohort otherWithSameName,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        otherWithSameName.Id = Guid.NewGuid(); // a different cohort owns the name
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns(otherWithSameName);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<EditCohortViewModel>(view.Model);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_RepositoryThrows_ReturnsViewWithEditViewModel(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        model.Id = id;
        model.MigrationPathSelection = "1";
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns((OrganizationPlanMigrationCohort?)null);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .ReplaceAsync(Arg.Any<OrganizationPlanMigrationCohort>())
            .ThrowsAsync(new InvalidOperationException("simulated failure"));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<EditCohortViewModel>(view.Model);
    }

    [Theory, BitAutoData]
    public async Task Delete_NonPendingAboveZero_RefusesAndRedirectsToEdit(
        Guid id,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        cohort.Id = id;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(cohort);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(3));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Delete(id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrganizationPlanMigrationCohortsController.Edit), redirect.ActionName);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceive()
            .DeleteAsync(Arg.Any<OrganizationPlanMigrationCohort>());
    }

    [Theory, BitAutoData]
    public async Task Delete_NoNonPending_DeletesAndRedirectsToIndex(
        Guid id,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        cohort.Id = id;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(cohort);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Delete(id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrganizationPlanMigrationCohortsController.Index), redirect.ActionName);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1).DeleteAsync(cohort);
    }

    [Theory, BitAutoData]
    public async Task Index_FeatureFlagDisabled_ReturnsNotFound(
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(false);

        var result = await sutProvider.Sut.Index();

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_LockedCohort_SetsAssignmentStateOnViewModel(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(3));

        var result = await sutProvider.Sut.Edit(cohort.Id);

        var view = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<EditCohortViewModel>(view.Model);
        Assert.True(viewModel.AssignmentState.HasNonPendingAssignments);
        Assert.Equal(3, viewModel.AssignmentState.NonPendingAssignmentCount);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_LockedCohort_IgnoresSubmittedMigrationPathSelection(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        existing.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        existing.RevisionDate = DateTime.UtcNow.AddDays(-1);
        model.Id = id;
        model.MigrationPathSelection = "none"; // operator (or attacker) tries to change it
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(7));
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns(existing);

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohort>(c =>
                c.Id == id
                && c.MigrationPathId == MigrationPathId.Enterprise2020AnnualToCurrent
                && c.Name == model.Name));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_UnlockedCohort_AcceptsMigrationPathSelectionChange(
        Guid id,
        CohortFormModel model,
        OrganizationPlanMigrationCohort existing,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        existing.Id = id;
        existing.MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent;
        existing.RevisionDate = DateTime.UtcNow.AddDays(-1);
        model.Id = id;
        model.MigrationPathSelection = "none"; // change attempt — should succeed because unlocked
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = "SAVE15";

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns(existing);
        sutProvider.GetDependency<IGetCohortAssignmentStateQuery>()
            .Run(Arg.Any<OrganizationPlanMigrationCohort>()).Returns(new CohortAssignmentState(0));
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByNameAsync(model.Name).Returns(existing);

        sutProvider.Sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPlanMigrationCohort>(c =>
                c.Id == id && c.MigrationPathId == null));
    }
}
