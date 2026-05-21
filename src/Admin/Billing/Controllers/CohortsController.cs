using Bit.Admin.Billing.Models.Cohorts;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("cohorts")]
public class CohortsController(
    IOrganizationPlanMigrationCohortRepository cohortRepository) : Controller
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

    private static string? NormalizeCouponCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
