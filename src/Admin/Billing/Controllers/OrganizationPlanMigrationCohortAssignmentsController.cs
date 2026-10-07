using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohortAssignments;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("migration-cohort-assignments")]
public class OrganizationPlanMigrationCohortAssignmentsController(
    IBulkSyncCohortAssignmentsCommand bulkSyncCommand,
    IFeatureService featureService) : Controller
{
    private bool FeatureEnabled() =>
        featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration);

    [HttpGet("")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public IActionResult Index()
    {
        if (!FeatureEnabled())
        {
            return NotFound();
        }

        return View(new BulkAssignmentUploadModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Upload(BulkAssignmentUploadModel model)
    {
        if (!FeatureEnabled())
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var result = await bulkSyncCommand.Run(model.File);

        IActionResult Reject(string message)
        {
            ModelState.AddModelError(string.Empty, message);
            return View("Index", model);
        }

        return result.Match<IActionResult>(
            value =>
            {
                if (!value.Succeeded)
                {
                    model.Errors = value.Errors.OrderBy(e => e.LineNumber).ToList();
                    return View("Index", model);
                }

                TempData["Success"] = "Cohort assignments updated.";
                return View("Result", new BulkAssignmentResultModel { Summary = value.Summary! });
            },
            badRequest => Reject(badRequest.Response),
            conflict => Reject(conflict.Response),
            unhandled => Reject(unhandled.Response));
    }
}
