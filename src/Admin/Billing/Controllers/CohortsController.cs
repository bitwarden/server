using Bit.Admin.Billing.Models.Cohorts;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
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
}
