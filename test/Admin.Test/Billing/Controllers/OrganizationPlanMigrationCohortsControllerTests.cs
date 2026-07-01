using System.Globalization;
using System.Text;
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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
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
    public async Task Edit_Post_LockedCohort_SavesDespiteMissingMigrationPathSelectionBindingError(
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
        model.ProactiveDiscountCouponCode = null;
        model.ChurnDiscountCouponCode = null;

        // The locked Edit view posts no value for MigrationPathSelection, so model binding leaves
        // it empty and the [Required] validator records an error before the action runs.
        model.MigrationPathSelection = string.Empty;
        sutProvider.Sut.ModelState.AddModelError(
            nameof(CohortFormModel.MigrationPathSelection), "Please select a migration path or None.");

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

    // --- Export ---

    // The Export action disposes its StreamWriter/CsvWriter, which closes the underlying
    // Response.Body. In production that stream is the real response; in the test we need to read
    // it back afterward, so we use a MemoryStream that survives Dispose.
    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing) { /* keep buffer readable */ }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public byte[] Captured => GetBuffer()[..(int)Length];
    }

    private static NonClosingMemoryStream WithResponseBody(
        OrganizationPlanMigrationCohortsController sut)
    {
        var body = new NonClosingMemoryStream();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = body;
        sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return body;
    }

    // DefaultHttpContext is sealed and HttpContext.Abort() delegates to the request-lifetime feature,
    // so we record the abort by swapping in a custom feature rather than subclassing. Backs the test
    // that a mid-stream failure aborts rather than silently truncating.
    private sealed class AbortTrackingLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public bool Aborted { get; private set; }
        public CancellationToken RequestAborted { get; set; }
        public void Abort() => Aborted = true;
    }

    private static AbortTrackingLifetimeFeature WithAbortTrackingResponseBody(
        OrganizationPlanMigrationCohortsController sut)
    {
        var lifetime = new AbortTrackingLifetimeFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        httpContext.Response.Body = new NonClosingMemoryStream();
        sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return lifetime;
    }

    private static string ReadBody(NonClosingMemoryStream body) =>
        Encoding.UTF8.GetString(body.Captured);

    private static async IAsyncEnumerable<CohortAssignmentExportRow> AsAsync(
        IEnumerable<CohortAssignmentExportRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    // Yields the supplied rows, then throws -- simulating a failure (DB read, serialization, write)
    // that surfaces after the response has already started streaming.
    private static async IAsyncEnumerable<CohortAssignmentExportRow> AsAsyncThenThrow(
        IEnumerable<CohortAssignmentExportRow> rows,
        Exception toThrow)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
        throw toThrow;
    }

    [Theory, BitAutoData]
    public async Task Export_FlagDisabled_ReturnsNotFound(
        Guid id,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(false);

        var result = await sutProvider.Sut.Export(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Export_CohortNotFound_ReturnsNotFound(
        Guid id,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(id).Returns((OrganizationPlanMigrationCohort?)null);

        var result = await sutProvider.Sut.Export(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Export_SetsCsvContentTypeAndSanitizedFilename(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        cohort.Name = "Enterprise, Q3/2026 \"big\" push";
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(Array.Empty<CohortAssignmentExportRow>()));

        WithResponseBody(sutProvider.Sut);
        var result = await sutProvider.Sut.Export(cohort.Id);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("text/csv", sutProvider.Sut.Response.ContentType);

        var disposition = sutProvider.Sut.Response.Headers.ContentDisposition.ToString();
        var expectedName = $"enterprise-q3-2026-big-push-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        Assert.Equal($"attachment; filename=\"{expectedName}\"", disposition);
        // No CRLF or quote characters leaked from the operator-entered name into the header.
        Assert.DoesNotContain('\r', disposition);
        Assert.DoesNotContain('\n', disposition);
    }

    [Theory, BitAutoData]
    public async Task Export_EmptyCohort_WritesHeaderOnly(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(Array.Empty<CohortAssignmentExportRow>()));

        var body = WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        var lines = ReadBody(body)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal("OrganizationId,OrganizationName,AssignedDate,ScheduledDate,MigratedDate", lines[0].TrimEnd('\r'));
    }

    [Theory, BitAutoData]
    public async Task Export_WritesHeaderAndRows(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        var assignedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var scheduledDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: "Acme Corp",
            AssignedDate: assignedAt,
            ScheduledDate: scheduledDate,
            MigratedDate: null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(new[] { row }));

        var body = WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        var lines = ReadBody(body).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("OrganizationId,OrganizationName,AssignedDate,ScheduledDate,MigratedDate", lines[0].TrimEnd('\r'));

        var fields = lines[1].TrimEnd('\r').Split(',');
        Assert.Equal(row.OrganizationId.ToString(), fields[0]);
        Assert.Equal("Acme Corp", fields[1]);
        Assert.Equal(assignedAt.ToString("o", CultureInfo.InvariantCulture), fields[2]);
        Assert.Equal(scheduledDate.ToString("o", CultureInfo.InvariantCulture), fields[3]);
        Assert.Equal(string.Empty, fields[4]);
    }

    [Theory, BitAutoData]
    public async Task Export_OrganizationName_FormulaInjectionPrefixed(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: "=cmd|'/c calc'!A1",
            AssignedDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledDate: null,
            MigratedDate: null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(new[] { row }));

        var body = WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        var content = ReadBody(body);
        // The dangerous leading '=' must be neutralized with a single-quote prefix. CsvHelper then
        // quotes the field because it now contains characters needing escaping.
        Assert.Contains("'=cmd", content);
        // The raw, unprefixed formula must NOT appear at the start of a field.
        Assert.DoesNotContain(",=cmd", content);
    }

    [Theory]
    [BitAutoData("=danger", true)]
    [BitAutoData("+danger", true)]
    [BitAutoData("-danger", true)]
    [BitAutoData("@danger", true)]
    [BitAutoData("\tdanger", true)]
    [BitAutoData("\rdanger", true)]
    [BitAutoData("Acme Corp", false)]
    public async Task Export_OrganizationName_SanitizesOnlyFormulaTriggers(
        string organizationName,
        bool expectPrefixed,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: organizationName,
            AssignedDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledDate: null,
            MigratedDate: null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(new[] { row }));

        var body = WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        var fields = ReadBody(body)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[1]
            .TrimEnd('\r')
            // The name field is the second column; CsvHelper quotes it when prefixed, so strip quotes.
            .Split(',')[1]
            .Trim('"');

        Assert.Equal(expectPrefixed ? "'" + organizationName : organizationName, fields);
    }

    [Theory]
    [BitAutoData("!!!")]
    [BitAutoData("   ")]
    [BitAutoData("日本語")]
    [BitAutoData("")]
    public async Task Export_NameSlugsToEmpty_FallsBackToCohortFilename(
        string cohortName,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        cohort.Name = cohortName;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(Array.Empty<CohortAssignmentExportRow>()));

        WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        var disposition = sutProvider.Sut.Response.Headers.ContentDisposition.ToString();
        var expectedName = $"cohort-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        Assert.Equal($"attachment; filename=\"{expectedName}\"", disposition);
    }

    [Theory, BitAutoData]
    public async Task Export_LogsActorAndCohortId_NeverOrgName(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        const string secretOrgName = "Top Secret Org Name";
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: secretOrgName,
            AssignedDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledDate: null,
            MigratedDate: null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsync(new[] { row }));

        var logger = sutProvider.GetDependency<ILogger<OrganizationPlanMigrationCohortsController>>();

        WithResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        // The audit log must record the cohort id (the actor is also included via the log template),
        // and must NEVER leak an organization name or CSV contents (zero-knowledge logging rule).
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(cohort.Id.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(secretOrgName)),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Export_QueryThrowsMidStream_AbortsAndLogsError(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: "Acme Corp",
            AssignedDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledDate: null,
            MigratedDate: null);
        var failure = new InvalidOperationException("read replica went away mid-stream");

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsyncThenThrow(new[] { row }, failure));

        var logger = sutProvider.GetDependency<ILogger<OrganizationPlanMigrationCohortsController>>();

        var lifetime = WithAbortTrackingResponseBody(sutProvider.Sut);
        await sutProvider.Sut.Export(cohort.Id);

        // The connection must be aborted so the operator gets a visibly broken download rather than
        // a silently truncated CSV that looks complete.
        Assert.True(lifetime.Aborted);

        // The failure must be logged at error with the cohort id and rows-written context, and must
        // carry the original exception so the cause is diagnosable.
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(cohort.Id.ToString())),
            failure,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task Export_ClientCancelsMidStream_AbortsAndLogsInformationNotError(
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationPlanMigrationCohortsController> sutProvider)
    {
        var row = new CohortAssignmentExportRow(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: "Acme Corp",
            AssignedDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ScheduledDate: null,
            MigratedDate: null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IExportCohortAssignmentsQuery>()
            .GetByCohortIdAsync(cohort.Id)
            .Returns(AsAsyncThenThrow(new[] { row }, new OperationCanceledException()));

        var logger = sutProvider.GetDependency<ILogger<OrganizationPlanMigrationCohortsController>>();

        var lifetime = WithAbortTrackingResponseBody(sutProvider.Sut);
        // Simulate the operator cancelling the download (closing the tab / hitting stop).
        lifetime.RequestAborted = new CancellationToken(canceled: true);

        await sutProvider.Sut.Export(cohort.Id);

        Assert.True(lifetime.Aborted);

        // A client cancel is expected, not a failure -- it must be logged at information so it does
        // not surface as an error in our dashboards.
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("cancelled")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
