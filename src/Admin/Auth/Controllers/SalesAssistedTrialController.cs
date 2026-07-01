using Bit.Admin.Auth.Models.SalesAssistedTrial;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.TrialInitiation.Registration;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Auth.Controllers;

// Lives in the Auth area because its primary purpose is registering new users in environments
// where open registration is disabled (globalSettings__disableUserRegistration=true). The
// sales-assisted token is the authorization for that constraint — this is an auth concern
// first, an organization/billing concern second.
//
// This differs from the existing trial-initiation buttons on the org edit page
// (OrganizationsController.Edit), which act on an already-existing organization for a
// prospect who already has an account. This controller handles the earlier step: inviting a
// prospect who has no account yet, in an environment where they cannot self-register.
[Authorize]
[Route("sales-assisted-trial")]
public class SalesAssistedTrialController(
    ISendSalesAssistedTrialInvitationCommand sendCommand,
    ILogger<SalesAssistedTrialController> logger) : Controller
{
    [HttpGet]
    [RequirePermission(Permission.Org_InitiateSalesAssistedTrial)]
    public IActionResult Index()
    {
        return View(new SalesTrialInviteModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_InitiateSalesAssistedTrial)]
    public async Task<IActionResult> Index(SalesTrialInviteModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var senderEmail = User.Identity!.Name!;

        try
        {
            await sendCommand.HandleAsync(
                model.Email,
                model.Name,
                senderEmail,
                model.ProductTier,
                model.Products,
                model.TrialLength,
                model.PaymentOptional);
        }
        catch (BadRequestException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending sales-assisted trial invitation.");
            ModelState.AddModelError(string.Empty, "An error occurred while sending the invitation.");
            return View(model);
        }

        logger.LogInformation(
            "Sales-assisted trial invitation sent by {SenderEmail} to {ProspectEmail}.",
            senderEmail,
            model.Email);

        TempData["Success"] = "Invitation sent.";
        return RedirectToAction(nameof(Index));
    }
}
