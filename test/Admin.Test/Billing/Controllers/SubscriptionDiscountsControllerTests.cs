using Bit.Admin.Billing.Controllers;
using Bit.Admin.Billing.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;

namespace Admin.Test.Billing.Controllers;

[ControllerCustomize(typeof(SubscriptionDiscountsController))]
[SutProviderCustomize]
public class SubscriptionDiscountsControllerTests
{
    [Theory, BitAutoData]
    public async Task Index_DefaultParameters_ReturnsViewWithDiscounts(
        List<SubscriptionDiscount> discounts,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .ListAsync(0, 25)
            .Returns(discounts);

        var result = await sutProvider.Sut.Index();

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        var model = Assert.IsType<SubscriptionDiscountPagedModel>(viewResult.Model);
        Assert.Equal(25, model.Count);
        Assert.Equal(1, model.Page);
        Assert.Equal(discounts.Count, model.Items.Count);
    }

    [Theory, BitAutoData]
    public async Task Index_WithPagination_CalculatesCorrectSkip(
        List<SubscriptionDiscount> discounts,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .ListAsync(50, 25)
            .Returns(discounts);

        var result = await sutProvider.Sut.Index(page: 3, count: 25);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .ListAsync(50, 25);
    }

    [Theory, BitAutoData]
    public async Task Index_WithInvalidPage_DefaultsToPage1(
        List<SubscriptionDiscount> discounts,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .ListAsync(0, 25)
            .Returns(discounts);

        var result = await sutProvider.Sut.Index(page: -1, count: 25);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .ListAsync(0, 25);
    }

    [Theory, BitAutoData]
    public void Create_Get_ReturnsViewWithEmptyModel(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var result = sutProvider.Sut.Create();

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        var model = Assert.IsType<CreateSubscriptionDiscountModel>(viewResult.Model);
        Assert.False(model.IsImported);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_ValidCoupon_ReturnsViewWithStripeProperties(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var stripeCoupon = new Stripe.Coupon
        {
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once"
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Returns(stripeCoupon);

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        Assert.Equal("Create", viewResult.ViewName);
        var returnedModel = Assert.IsType<CreateSubscriptionDiscountModel>(viewResult.Model);
        Assert.Equal(stripeCoupon.Name, returnedModel.Name);
        Assert.Equal(stripeCoupon.PercentOff, returnedModel.PercentOff);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_ValidCoupon_SetsIsImportedToTrue(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var stripeCoupon = new Stripe.Coupon
        {
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once"
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Returns(stripeCoupon);

        // Ensure IsImported starts as false
        model.IsImported = false;

        var result = await sutProvider.Sut.ImportCoupon(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        var returnedModel = Assert.IsType<CreateSubscriptionDiscountModel>(viewResult.Model);
        Assert.True(returnedModel.IsImported, "IsImported should be set to true after successful import");
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_CouponWithProductRestrictions_MapsProductIds(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var productIds = new List<string> { "prod_test1", "prod_test2", "prod_test3" };
        var stripeCoupon = new Stripe.Coupon
        {
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once",
            AppliesTo = new Stripe.CouponAppliesTo
            {
                Products = productIds
            }
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Returns(stripeCoupon);

        var products = new List<Stripe.Product>
        {
            new() { Id = "prod_test1", Name = "Test Product 1" },
            new() { Id = "prod_test2", Name = "Test Product 2" },
            new() { Id = "prod_test3", Name = "Test Product 3" }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .ListProductsAsync(Arg.Is<ProductListOptions>(o =>
                o.Ids != null &&
                o.Ids.Count == 3 &&
                o.Ids.Contains("prod_test1") &&
                o.Ids.Contains("prod_test2") &&
                o.Ids.Contains("prod_test3")))
            .Returns(products);

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        var returnedModel = Assert.IsType<CreateSubscriptionDiscountModel>(viewResult.Model);
        Assert.NotNull(returnedModel.AppliesToProducts);
        Assert.Equal(3, returnedModel.AppliesToProducts.Count);
        Assert.Equal("Test Product 1", returnedModel.AppliesToProducts["prod_test1"]);
        Assert.Equal("Test Product 2", returnedModel.AppliesToProducts["prod_test2"]);
        Assert.Equal("Test Product 3", returnedModel.AppliesToProducts["prod_test3"]);

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .ListProductsAsync(Arg.Is<ProductListOptions>(o => o.Ids != null && o.Ids.Count == 3));
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_EmptyCouponId_ReturnsViewWithError(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel { StripeCouponId = "" };
        sutProvider.Sut.ModelState.AddModelError(nameof(model.StripeCouponId), "The Stripe Coupon ID field is required.");

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("required", sutProvider.Sut.ModelState[nameof(model.StripeCouponId)]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_DuplicateCoupon_ReturnsViewWithError(
        CreateSubscriptionDiscountModel model,
        SubscriptionDiscount existingDiscount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns(existingDiscount);

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("already been imported", sutProvider.Sut.ModelState[nameof(model.StripeCouponId)]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_StripeApiError_ReturnsViewWithError(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Throws(new StripeException());

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("error occurred", sutProvider.Sut.ModelState[nameof(model.StripeCouponId)]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_StripeResourceMissingError_ReturnsViewWithSpecificError(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        var stripeError = new StripeError { Code = "resource_missing" };
        var stripeException = new StripeException { StripeError = stripeError };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Throws(stripeException);

        var result = await sutProvider.Sut.ImportCoupon(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("not found in Stripe", sutProvider.Sut.ModelState[nameof(model.StripeCouponId)]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task ImportCoupon_WithDurationInMonths_ConvertsToInt(
        CreateSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var stripeCoupon = new Stripe.Coupon
        {
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "repeating",
            DurationInMonths = 12L
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.GetDependency<IStripeAdapter>()
            .GetCouponAsync(Arg.Any<string>(), Arg.Any<CouponGetOptions>())
            .Returns(stripeCoupon);

        var result = await sutProvider.Sut.ImportCoupon(model);

        var viewResult = (ViewResult)result;
        var returnedModel = Assert.IsType<CreateSubscriptionDiscountModel>(viewResult.Model);
        Assert.Equal(12, returnedModel.DurationInMonths);
    }

    [Theory, BitAutoData]
    public async Task Create_ValidModel_CreatesDiscountAndRedirects(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel
        {
            StripeCouponId = "TEST123",
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            RestrictToNewUsersOnly = false,
            IsImported = true
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.Sut.ModelState.Clear();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<RedirectToActionResult>(result);
        var redirectResult = (RedirectToActionResult)result;
        Assert.Equal(nameof(SubscriptionDiscountsController.Index), redirectResult.ActionName);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<SubscriptionDiscount>(d =>
                d.StripeCouponId == model.StripeCouponId &&
                d.Name == model.Name &&
                d.StartDate == model.StartDate &&
                d.EndDate == model.EndDate &&
                d.AudienceType == DiscountAudienceType.AllUsers));
    }

    [Theory, BitAutoData]
    public async Task Create_WithRestrictToNewUsersOnly_SetsCorrectAudienceType(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel
        {
            StripeCouponId = "TEST123",
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            RestrictToNewUsersOnly = true,
            IsImported = true
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns((SubscriptionDiscount?)null);

        sutProvider.Sut.ModelState.Clear();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        var result = await sutProvider.Sut.Create(model);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<SubscriptionDiscount>(d =>
                d.AudienceType == DiscountAudienceType.UserHasNoPreviousSubscriptions));
    }

    [Theory, BitAutoData]
    public async Task Create_NotImported_ReturnsViewWithError(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel
        {
            StripeCouponId = "TEST123",
            Name = null
        };

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("import the coupon", sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task Create_DuplicateCoupon_ReturnsViewWithError(
        SubscriptionDiscount existingDiscount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel
        {
            StripeCouponId = "TEST123",
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            IsImported = true
        };

        // Simulate race condition: another admin imported the same coupon between import and save
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(model.StripeCouponId)
            .Returns(existingDiscount);

        sutProvider.Sut.ModelState.Clear();

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("already been imported", sutProvider.Sut.ModelState[nameof(model.StripeCouponId)]!.Errors[0].ErrorMessage);

        // Verify CreateAsync was NOT called since we detected the duplicate
        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<SubscriptionDiscount>());
    }

    [Theory, BitAutoData]
    public async Task Create_RepositoryThrowsException_ReturnsViewWithError(
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new CreateSubscriptionDiscountModel
        {
            StripeCouponId = "TEST123",
            Name = "Test Coupon",
            PercentOff = 25,
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            IsImported = true
        };

        sutProvider.Sut.ModelState.Clear();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .CreateAsync(Arg.Any<SubscriptionDiscount>())
            .Throws(new Exception("Database error"));

        var result = await sutProvider.Sut.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("error occurred", sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_ReturnsViewWithModel(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListProductsAsync(Arg.Any<ProductListOptions>())
            .Returns(new List<Stripe.Product>());

        var result = await sutProvider.Sut.Edit(discount.Id);

        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        var model = Assert.IsType<EditSubscriptionDiscountModel>(viewResult.Model);
        Assert.Equal(discount.Id, model.Id);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
        Assert.Equal(discount.StartDate, model.StartDate);
        Assert.Equal(discount.EndDate, model.EndDate);
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_WithStripeProducts_PopulatesAppliesToProducts(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        discount.StripeProductIds = new List<string> { "prod_1", "prod_2" };
        var stripeProducts = new List<Stripe.Product>
        {
            new() { Id = "prod_1", Name = "Product One" },
            new() { Id = "prod_2", Name = "Product Two" }
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListProductsAsync(Arg.Any<ProductListOptions>())
            .Returns(stripeProducts);

        var result = await sutProvider.Sut.Edit(discount.Id);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EditSubscriptionDiscountModel>(viewResult.Model);
        Assert.NotNull(model.AppliesToProducts);
        Assert.Equal(2, model.AppliesToProducts.Count);
        Assert.Equal("Product One", model.AppliesToProducts["prod_1"]);
        Assert.Equal("Product Two", model.AppliesToProducts["prod_2"]);
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_WhenStripeProductLookupFails_StillReturnsView(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        discount.StripeProductIds = new List<string> { "prod_1" };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListProductsAsync(Arg.Any<ProductListOptions>())
            .Throws(new StripeException());

        var result = await sutProvider.Sut.Edit(discount.Id);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EditSubscriptionDiscountModel>(viewResult.Model);
        Assert.Null(model.AppliesToProducts);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("Failed to fetch", sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task Edit_Get_WhenNotFound_ReturnsNotFound(
        Guid id,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(id)
            .Returns((SubscriptionDiscount?)null);

        var result = await sutProvider.Sut.Edit(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_ValidModel_UpdatesBitwardenFieldsAndRedirects(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var model = new EditSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date.AddDays(1),
            EndDate = DateTime.UtcNow.Date.AddMonths(2),
            RestrictToNewUsersOnly = true
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        var result = await sutProvider.Sut.Edit(discount.Id, model);

        Assert.IsType<RedirectToActionResult>(result);
        var redirectResult = (RedirectToActionResult)result;
        Assert.Equal(nameof(SubscriptionDiscountsController.Index), redirectResult.ActionName);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<SubscriptionDiscount>(d =>
                d.StartDate == model.StartDate &&
                d.EndDate == model.EndDate &&
                d.AudienceType == DiscountAudienceType.UserHasNoPreviousSubscriptions));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_ValidModel_DoesNotUpdateStripeFields(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        var originalStripeCouponId = discount.StripeCouponId;
        var originalPercentOff = discount.PercentOff;
        var originalAmountOff = discount.AmountOff;
        var originalDuration = discount.Duration;

        var model = new EditSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            RestrictToNewUsersOnly = false
        };

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        await sutProvider.Sut.Edit(discount.Id, model);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<SubscriptionDiscount>(d =>
                d.StripeCouponId == originalStripeCouponId &&
                d.PercentOff == originalPercentOff &&
                d.AmountOff == originalAmountOff &&
                d.Duration == originalDuration));
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_InvalidModelState_ReturnsView(
        Guid id,
        EditSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.Sut.ModelState.AddModelError(nameof(model.EndDate), "End Date must be on or after Start Date.");

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<ViewResult>(result);
        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<SubscriptionDiscount>());
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_RepositoryThrowsException_ReturnsViewWithError(
        SubscriptionDiscount discount,
        EditSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .ReplaceAsync(Arg.Any<SubscriptionDiscount>())
            .Throws(new Exception("Database error"));

        var result = await sutProvider.Sut.Edit(discount.Id, model);

        Assert.IsType<ViewResult>(result);
        Assert.False(sutProvider.Sut.ModelState.IsValid);
        Assert.Contains("error occurred", sutProvider.Sut.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Theory, BitAutoData]
    public async Task Edit_Post_WhenNotFound_ReturnsNotFound(
        Guid id,
        EditSubscriptionDiscountModel model,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(id)
            .Returns((SubscriptionDiscount?)null);

        var result = await sutProvider.Sut.Edit(id, model);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory, BitAutoData]
    public async Task Delete_Post_DeletesDiscountAndRedirectsToIndex(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        var result = await sutProvider.Sut.Delete(discount.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var redirectResult = (RedirectToActionResult)result;
        Assert.Equal(nameof(SubscriptionDiscountsController.Index), redirectResult.ActionName);

        await sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .Received(1)
            .DeleteAsync(discount);
    }

    [Theory, BitAutoData]
    public async Task Delete_Post_RepositoryThrowsException_RedirectsToEditWithError(
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(discount.Id)
            .Returns(discount);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .DeleteAsync(discount)
            .Throws(new Exception("Database error"));

        var tempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        sutProvider.Sut.TempData = tempData;

        var result = await sutProvider.Sut.Delete(discount.Id);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SubscriptionDiscountsController.Edit), redirectResult.ActionName);
        Assert.Contains("attempting to delete", sutProvider.Sut.TempData["Error"]!.ToString());
    }

    [Theory, BitAutoData]
    public async Task Delete_Post_WhenNotFound_ReturnsNotFound(
        Guid id,
        SutProvider<SubscriptionDiscountsController> sutProvider)
    {
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByIdAsync(id)
            .Returns((SubscriptionDiscount?)null);

        var result = await sutProvider.Sut.Delete(id);

        Assert.IsType<NotFoundResult>(result);
    }
}
