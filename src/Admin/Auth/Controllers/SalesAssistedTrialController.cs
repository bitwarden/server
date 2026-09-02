using Bit.Admin.Auth.Models.SalesAssistedTrial;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.Enums;
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
        // Defaults reflect the most common sales-assisted trial shape. They are applied only
        // here, on the initial GET — never as property initializers on the model itself — so a
        // POST redisplay always reflects exactly what the user submitted, never a reasserted
        // default.
        return View(new SalesAssistedTrialInviteModel
        {
            ProductTier = ProductTierType.Enterprise,
            Product = ProductType.PasswordManager,
            TrialLength = 30
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_InitiateSalesAssistedTrial)]
    public async Task<IActionResult> Index(SalesAssistedTrialInviteModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var senderEmail = User.Identity!.Name!;

        try
        {
            // Client /trial-initiation prioritizes a single product via parseInt
            // and Secrets Manager trial initiation also provides Password Manager trial
            // initiation at the selected product tier. PM-41426
            // Self-service trial command has an IEnumerable shape for Product(s), however
            // to ensure proper parsing and enrollment, sales-assisted will enforce a
            // single product selection policy.
            await sendCommand.HandleAsync(
                model.Email,
                model.Name,
                senderEmail,
                model.ProductTier,
                new[] { model.Product },
                model.TrialLength);
        }
        catch (BadRequestException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending sales-assisted trial invitation. Exception Type: {ExceptionType}", ex.GetType().Name);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        TempData["Success"] = "Invitation sent.";
        return RedirectToAction(nameof(Index));
    }
}
