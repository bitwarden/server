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
    public async Task<IActionResult> Index()
    {
        var items = await cohortRepository.SearchWithCountsAsync(null, 0, DefaultPageSize);

        return View(new CohortsPagedModel
        {
            Items = items.Select(CohortListItemViewModel.From).ToList(),
        });
    }
}
