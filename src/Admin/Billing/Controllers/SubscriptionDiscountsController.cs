using Bit.Admin.Billing.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("subscription-discounts")]
public class SubscriptionDiscountsController(
    ISubscriptionDiscountRepository subscriptionDiscountRepository,
    IStripeAdapter stripeAdapter,
    ILogger<SubscriptionDiscountsController> logger) : Controller
{
    private const string SuccessKey = "Success";
    private const string ErrorKey = "Error";

    [HttpGet]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> Index(int page = 1, int count = 25)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (count < 1)
        {
            count = 1;
        }

        var skip = (page - 1) * count;
        var discounts = await subscriptionDiscountRepository.ListAsync(skip, count);

        var discountViewModels = discounts.Select(d => new SubscriptionDiscountViewModel
        {
            Id = d.Id,
            StripeCouponId = d.StripeCouponId,
            Name = d.Name,
            PercentOff = d.PercentOff,
            AmountOff = d.AmountOff,
            Currency = d.Currency,
            Duration = d.Duration,
            DurationInMonths = d.DurationInMonths,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            AudienceType = d.AudienceType,
            CreationDate = d.CreationDate
        }).ToList();

        var model = new SubscriptionDiscountPagedModel
        {
            Items = discountViewModels,
            Page = page,
            Count = count
        };

        return View(model);
    }

    [HttpGet("create")]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public IActionResult Create()
    {
        return View(new CreateSubscriptionDiscountModel());
    }

    [HttpPost("import-coupon")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> ImportCoupon(CreateSubscriptionDiscountModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Create", model);
        }

        try
        {
            var existing = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(model.StripeCouponId);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.StripeCouponId),
                    "This coupon has already been imported.");
                return View("Create", model);
            }

            Coupon coupon;
            try
            {
                var options = new CouponGetOptions();
                options.AddExpand(StripeConstants.CouponExpandablePropertyNames.AppliesTo);
                coupon = await stripeAdapter.GetCouponAsync(model.StripeCouponId, options);
            }
            catch (StripeException ex)
            {
                var errorMessage = ex.StripeError?.Code == "resource_missing"
                    ? "Coupon not found in Stripe. Please verify the coupon ID."
                    : "An error occurred while fetching the coupon from Stripe.";

                logger.LogError(ex, "Stripe coupon error: {CouponId}", model.StripeCouponId);
                ModelState.AddModelError(nameof(model.StripeCouponId), errorMessage);
                return View("Create", model);
            }

            model.Name = coupon.Name;
            model.PercentOff = coupon.PercentOff;
            model.AmountOff = coupon.AmountOff;
            model.Currency = coupon.Currency;
            model.Duration = coupon.Duration;
            model.DurationInMonths = (int?)coupon.DurationInMonths;

            var productIds = coupon.AppliesTo?.Products;
            if (productIds != null && productIds.Count != 0)
            {
                try
                {
                    var allProducts = await stripeAdapter.ListProductsAsync(new ProductListOptions
                    {
                        Ids = productIds.ToList()
                    });

                    model.AppliesToProducts = allProducts
                        .ToDictionary(product => product.Id, product => product.Name);
                }
                catch (StripeException ex)
                {
                    logger.LogError(ex, "Failed to fetch the coupon's associated products from Stripe. Coupon ID: {CouponId}", model.StripeCouponId);
                    ModelState.AddModelError(string.Empty, "Failed to fetch the coupon's associated products from Stripe.");
                    return View("Create", model);
                }
            }

            model.IsImported = true;
            return View("Create", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing coupon from Stripe. Coupon ID: {CouponId}", model.StripeCouponId);
            ModelState.AddModelError(string.Empty, "An error occurred while importing the coupon.");
            return View("Create", model);
        }
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> Create(CreateSubscriptionDiscountModel model)
    {
        if (!model.IsImported)
        {
            ModelState.AddModelError(string.Empty,
                "Please import the coupon from Stripe before submitting.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Check for duplicate coupon to prevent race condition
            var existing = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(model.StripeCouponId);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.StripeCouponId),
                    "This coupon has already been imported.");
                return View(model);
            }

            var discount = new SubscriptionDiscount
            {
                StripeCouponId = model.StripeCouponId,
                Name = model.Name,
                PercentOff = model.PercentOff,
                AmountOff = model.AmountOff,
                Currency = model.Currency,
                Duration = model.Duration,
                DurationInMonths = model.DurationInMonths,
                StripeProductIds = model.AppliesToProducts?.Keys.ToList(),
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                AudienceType = model.AudienceType,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };

            await subscriptionDiscountRepository.CreateAsync(discount);

            PersistSuccessMessage($"Discount '{model.StripeCouponId}' imported successfully.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating subscription discount. Coupon ID: {CouponId}", model.StripeCouponId);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the discount.");
            return View(model);
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var discount = await subscriptionDiscountRepository.GetByIdAsync(id);
        if (discount == null)
        {
            return NotFound();
        }

        var model = new EditSubscriptionDiscountModel(discount);

        if (model.StripeProductIds is { Count: > 0 })
        {
            try
            {
                var products = await stripeAdapter.ListProductsAsync(new ProductListOptions
                {
                    Ids = model.StripeProductIds.ToList()
                });
                model.AppliesToProducts = products.ToDictionary(p => p.Id, p => p.Name);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Failed to fetch the coupon's associated products from Stripe. Coupon ID: {CouponId}", model.StripeCouponId);
                ModelState.AddModelError(string.Empty, "Failed to fetch the coupon's associated products from Stripe. However, editing is still possible.");
            }
        }

        return View(model);
    }

    [HttpPost("{id}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> Edit(Guid id, EditSubscriptionDiscountModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var discount = await subscriptionDiscountRepository.GetByIdAsync(id);
        if (discount == null)
        {
            return NotFound();
        }

        try
        {
            discount.StartDate = model.StartDate;
            discount.EndDate = model.EndDate;
            discount.AudienceType = model.AudienceType;
            discount.RevisionDate = DateTime.UtcNow;

            await subscriptionDiscountRepository.ReplaceAsync(discount);

            PersistSuccessMessage($"Discount '{discount.StripeCouponId}' updated successfully.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating subscription discount. Coupon ID: {CouponId}", discount.StripeCouponId);
            ModelState.AddModelError(string.Empty, "An error occurred while updating the discount.");
            return View(model);
        }
    }

    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_CreateEditTransaction)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var discount = await subscriptionDiscountRepository.GetByIdAsync(id);
        if (discount == null)
        {
            return NotFound();
        }

        try
        {
            await subscriptionDiscountRepository.DeleteAsync(discount);

            PersistSuccessMessage($"Discount '{discount.StripeCouponId}' deleted successfully.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting subscription discount. Coupon ID: {CouponId}", discount.StripeCouponId);
            PersistErrorMessage("An error occurred while attempting to delete the discount.");
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    private void PersistSuccessMessage(string message) => TempData[SuccessKey] = message;
    private void PersistErrorMessage(string message) => TempData[ErrorKey] = message;
}
