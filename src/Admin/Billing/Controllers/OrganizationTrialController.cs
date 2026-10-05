using Bit.Admin.Billing.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("organizations/billing/{organizationId:guid}/trial")]
[SelfHosted(NotSelfHostedOnly = true)]
public class OrganizationTrialController(
    IOrganizationRepository organizationRepository,
    IExtendOrganizationTrialCommand extendOrganizationTrialCommand,
    IFeatureService featureService,
    ILogger<OrganizationTrialController> logger) : Controller
{
    private const string _genericError = "The trial could not be extended. Please try again or contact support if the problem persists.";

    [HttpPost("extend")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_ExtendTrial)]
    public async Task<IActionResult> ExtendAsync([FromRoute] Guid organizationId, ExtendTrialModel model)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PM35092AuthSalesAssistedTrials))
        {
            return NotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.GetErrorMessage();
            return RedirectToEdit(organizationId);
        }

        var days = model.Days!.Value;
        var result = await extendOrganizationTrialCommand.Run(organization, days);

        result.Switch(
            newTrialEnd =>
            {
                // Audit record for FedRAMP: who extended which organization's trial, by how much, and the resulting dates.
                // The actor is the authenticated admin portal identity, never a form value.
                logger.LogInformation(
                    "Trial extended by {Actor} for organization ({OrganizationId}) by {Days} days: trial end moved from {PreviousTrialEnd:u} to {NewTrialEnd:u}",
                    User?.Identity?.Name ?? "unknown", organization.Id, days, newTrialEnd.AddDays(-days), newTrialEnd);
                TempData["Success"] = $"Trial extended to {newTrialEnd:yyyy-MM-dd} UTC.";
            },
            badRequest => TempData["Error"] = badRequest.Response,
            conflict =>
            {
                LogFailedAttempt(organization.Id, days, conflict.Response, exception: null);
                TempData["Error"] = conflict.Response;
            },
            unhandled =>
            {
                LogFailedAttempt(organization.Id, days, unhandled.Response, unhandled.Exception);
                TempData["Error"] = _genericError;
            });

        return RedirectToEdit(organizationId);
    }

    private void LogFailedAttempt(Guid organizationId, int days, string reason, Exception? exception) =>
        logger.LogError(exception,
            "Trial extension by {Actor} for organization ({OrganizationId}) by {Days} days failed: {Reason}",
            User?.Identity?.Name ?? "unknown", organizationId, days, reason);

    private RedirectToActionResult RedirectToEdit(Guid organizationId) =>
        RedirectToAction("Edit", "Organizations", new { id = organizationId });
}
