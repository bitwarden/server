using System.ComponentModel.DataAnnotations;
using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("migration-cohorts")]
public class OrganizationPlanMigrationCohortsController(
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IStripeAdapter stripeAdapter,
    ILogger<OrganizationPlanMigrationCohortsController> logger,
    IFeatureService featureService,
    IGetCohortAssignmentStateQuery getCohortAssignmentStateQuery) : Controller
{
    private const int _defaultPageSize = 25;

    private bool PlanMigrationCohortsFeatureEnabled() =>
        featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration);

    [HttpGet("")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Index(string? name = null, int page = 1, int count = _defaultPageSize)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

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
    public IActionResult Create()
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        return View(new CohortFormModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Create(CohortFormModel model)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        MergeCrossFieldValidationErrors(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            if (!await ValidateNameAsync(model.Name) || !await ValidateCouponsAsync(model))
            {
                return View(model);
            }

            var cohort = new OrganizationPlanMigrationCohort
            {
                Name = model.Name,
                MigrationPathId = model.GetMigrationPathId(),
                ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode),
                ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode),
            };

            await cohortRepository.CreateAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating cohort. Name: {Name}", model.Name);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(model);
        }
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);
        return View(EditCohortViewModel.From(cohort, CohortFormModel.From(cohort), assignmentState));
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id, CohortFormModel model)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        model.Id = id;

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);

        if (assignmentState.HasNonPendingAssignments)
        {
            // The locked view doesn't post a value for MigrationPathSelection.
            // Restore from the persisted cohort so [Required] passes and the eventual
            // ReplaceAsync writes back the unchanged path.
            model.MigrationPathSelection = cohort.MigrationPathId switch
            {
                null => "none",
                var pathId => ((byte)pathId).ToString(),
            };
        }

        MergeCrossFieldValidationErrors(model);

        if (!ModelState.IsValid)
        {
            return View(EditCohortViewModel.From(cohort, model, assignmentState));
        }

        try
        {
            if (!await ValidateNameAsync(model.Name, id)
                || !await ValidateCouponsAsync(model))
            {
                return View(EditCohortViewModel.From(cohort, model, assignmentState));
            }

            cohort.Name = model.Name;
            cohort.MigrationPathId = model.GetMigrationPathId();
            cohort.ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode);
            cohort.ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode);
            cohort.IsActive = model.IsActive;
            cohort.RevisionDate = DateTime.UtcNow;

            await cohortRepository.ReplaceAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating cohort. Id: {Id}", id);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(EditCohortViewModel.From(cohort, model, assignmentState));
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        try
        {
            var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);
            if (assignmentState.HasNonPendingAssignments)
            {
                TempData["Error"] =
                    $"Cannot delete cohort '{cohort.Name}' because {assignmentState.NonPendingAssignmentCount:N0} " +
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

    private static string? NormalizeCouponCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // MVC skips IValidatableObject.Validate when any property-level attribute already failed, hiding cross-field
    // rules until the operator resubmits. Run it explicitly so every error surfaces on a single submit.
    // See https://github.com/dotnet/aspnetcore/issues/1899.
    private void MergeCrossFieldValidationErrors(CohortFormModel model)
    {
        foreach (var result in model.Validate(new ValidationContext(model)))
        {
            foreach (var memberName in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                ModelState.AddModelError(memberName, result.ErrorMessage ?? string.Empty);
            }
        }
    }

    private async Task<bool> ValidateNameAsync(string name, Guid? excludeId = null)
    {
        var existing = await cohortRepository.GetByNameAsync(name);
        if (existing == null || existing.Id == excludeId)
        {
            return true;
        }

        ModelState.AddModelError(nameof(CohortFormModel.Name), "A cohort with this name already exists.");
        return false;
    }

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
