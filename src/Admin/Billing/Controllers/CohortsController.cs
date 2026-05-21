using Bit.Admin.Billing.Models.Cohorts;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Billing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("cohorts")]
public class CohortsController(
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IStripeAdapter stripeAdapter,
    ILogger<CohortsController> logger,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository) : Controller
{
    private const int DefaultPageSize = 25;

    [HttpGet("")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Index(string? name = null, int page = 1, int count = DefaultPageSize)
    {
        if (page < 1) page = 1;
        if (count < 1) count = 1;
        var skip = (page - 1) * count;

        var items = await cohortRepository.SearchWithCountsAsync(name, skip, count);

        return View(new CohortsPagedModel
        {
            Name = name,
            Items = items.Select(CohortListItemViewModel.From).ToList(),
            Page = page,
            Count = count,
        });
    }

    [HttpGet("create")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public IActionResult Create() => View(new CohortFormModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Create(CohortFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var duplicate = await cohortRepository.GetByNameAsync(model.Name);
            if (duplicate != null)
            {
                ModelState.AddModelError(nameof(model.Name),
                    "A cohort with this name already exists.");
                return View(model);
            }

            if (!await ValidateCouponsAsync(model))
            {
                return View(model);
            }

            var cohort = new OrganizationPlanMigrationCohort
            {
                Name = model.Name,
                MigrationPathId = model.GetMigrationPathId(),
                ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode),
                ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode),
                IsActive = false,
            };

            await cohortRepository.CreateAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(model);
        }
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        ViewData["CohortType"] = CohortType.From(cohort.MigrationPathId);
        return View(ToFormModel(cohort));
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id, CohortFormModel model)
    {
        model.Id = id;

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        ViewData["CohortType"] = CohortType.From(cohort.MigrationPathId);

        if (!ModelState.IsValid) return View(model);

        try
        {
            var nameMatch = await cohortRepository.GetByNameAsync(model.Name);
            if (nameMatch != null && nameMatch.Id != id)
            {
                ModelState.AddModelError(nameof(model.Name),
                    "A cohort with this name already exists.");
                return View(model);
            }

            if (!await ValidateCouponsAsync(model)) return View(model);

            cohort.Name = model.Name;
            cohort.MigrationPathId = model.GetMigrationPathId();
            cohort.ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode);
            cohort.ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode);
            cohort.RevisionDate = DateTime.UtcNow;

            await cohortRepository.ReplaceAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating cohort. Id: {Id}", id);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        try
        {
            var nonPendingCount = await assignmentRepository.GetCohortNonPendingAssignmentsCountAsync(id);
            if (nonPendingCount > 0)
            {
                TempData["Error"] =
                    $"Cannot delete cohort '{cohort.Name}' because {nonPendingCount:N0} " +
                    "assignment(s) have left the Pending state. Historical migration and " +
                    "save-offer records are preserved.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            await cohortRepository.DeleteAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting cohort. Id: {Id}", id);
            TempData["Error"] = "An error occurred while attempting to delete the cohort.";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    private static CohortFormModel ToFormModel(OrganizationPlanMigrationCohort cohort) => new()
    {
        Id = cohort.Id,
        Name = cohort.Name,
        MigrationPathSelection = cohort.MigrationPathId switch
        {
            null => "none",
            var id => ((byte)id).ToString(),
        },
        ProactiveDiscountCouponCode = cohort.ProactiveDiscountCouponCode,
        ChurnDiscountCouponCode = cohort.ChurnDiscountCouponCode,
    };

    private static string? NormalizeCouponCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<bool> ValidateCouponsAsync(CohortFormModel model)
    {
        var proactive = NormalizeCouponCode(model.ProactiveDiscountCouponCode);
        var churn = NormalizeCouponCode(model.ChurnDiscountCouponCode);

        var ok = true;
        if (proactive != null && !await TryValidateCouponAsync(proactive, nameof(model.ProactiveDiscountCouponCode)))
        {
            ok = false;
        }
        if (churn != null && !await TryValidateCouponAsync(churn, nameof(model.ChurnDiscountCouponCode)))
        {
            ok = false;
        }
        return ok;
    }

    private async Task<bool> TryValidateCouponAsync(string couponId, string fieldName)
    {
        try
        {
            await stripeAdapter.GetCouponAsync(couponId);
            return true;
        }
        catch (StripeException ex)
        {
            var message = ex.StripeError?.Code == "resource_missing"
                ? "Coupon not found in Stripe. Please verify the coupon ID."
                : "An error occurred while fetching the coupon from Stripe.";

            logger.LogError(ex, "Stripe coupon error: {CouponId}", couponId);
            ModelState.AddModelError(fieldName, message);
            return false;
        }
    }
}
