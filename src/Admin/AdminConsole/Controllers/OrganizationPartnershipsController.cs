using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.AdminConsole.Controllers;

[Authorize]
[Route("organizations/{organizationId:guid}/partnership")]
public class OrganizationPartnershipsController(
    IOrganizationRepository organizationRepository,
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    ICreateOrganizationPartnershipCommand createOrganizationPartnershipCommand,
    Bitwarden.Server.Sdk.Features.IFeatureService featureService) : Controller
{
    private const string _alreadyExistsMessage = "This organization already has a partnership.";

    private bool FeatureEnabled() => featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships);

    [HttpGet("create")]
    [RequirePermission(Permission.Org_Plan_Edit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Create([FromRoute] Guid organizationId)
    {
        if (!FeatureEnabled())
        {
            return NotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
        {
            return NotFound();
        }

        if (await organizationPartnershipRepository.GetByOrganizationIdAsync(organizationId) is not null)
        {
            TempData["Error"] = _alreadyExistsMessage;
            return RedirectToOrganization(organizationId);
        }

        return View(new CreateOrganizationPartnershipModel
        {
            OrganizationId = organizationId,
            OrganizationName = organization.DisplayName(),
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Org_Plan_Edit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Create([FromRoute] Guid organizationId, CreateOrganizationPartnershipModel model)
    {
        if (!FeatureEnabled())
        {
            return NotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
        {
            return NotFound();
        }

        model.OrganizationId = organizationId;
        model.OrganizationName = organization.DisplayName();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await createOrganizationPartnershipCommand.CreateAsync(model.ToRequest(organizationId));
        if (result.IsSuccess)
        {
            TempData["Success"] = "Partnership created.";
            return RedirectToOrganization(organizationId);
        }

        var code = (result.AsError as IPartnershipError)?.Code;
        if (code == "not_found")
        {
            return NotFound();
        }

        var (key, message) = code switch
        {
            "invalid_name" => (nameof(model.Name),
                $"Name is required and must be at most {CreateOrganizationPartnershipRequest.NameMaxLength} characters."),
            "unsupported_binding_mode" => (nameof(model.BindingMode), "Only token binding is supported."),
            "invalid_return_origin" => (nameof(model.RegisteredReturnOrigins),
                "Each return origin must be an https origin such as https://partner.example.com, " +
                "with no path, query, fragment, credentials, or default port."),
            "partnership_already_exists" => (string.Empty, _alreadyExistsMessage),
            _ => (string.Empty, "The partnership could not be created."),
        };
        ModelState.AddModelError(key, message);
        return View(model);
    }

    private RedirectToActionResult RedirectToOrganization(Guid organizationId) =>
        RedirectToAction(nameof(OrganizationsController.Edit), "Organizations", new { id = organizationId });
}
