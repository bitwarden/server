#nullable enable
using Bit.Admin.Billing.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("organizations/billing/{organizationId:guid}/business-unit")]
public class BusinessUnitConversionController(
    IBusinessUnitConverter businessUnitConverter,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IProviderUserRepository providerUserRepository) : Controller
{
    [HttpGet]
    [RequirePermission(Permission.Org_Billing_ConvertToBusinessUnit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> IndexAsync([FromRoute] Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        var model = new BusinessUnitConversionModel { Organization = organization };

        var invitedProviderAdmin = await GetInvitedProviderAdminAsync(organization);

        if (invitedProviderAdmin != null)
        {
            model.ProviderAdminEmail = invitedProviderAdmin.Email;
            model.ProviderId = invitedProviderAdmin.ProviderId;
        }

        var success = ReadSuccessMessage();

        if (!string.IsNullOrEmpty(success))
        {
            model.Success = success;
        }

        var errors = ReadErrorMessages();

        if (errors is { Count: > 0 })
        {
            model.Errors = errors;
        }

        return View(model);
    }

    [HttpPost]
    [RequirePermission(Permission.Org_Billing_ConvertToBusinessUnit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> InitiateAsync(
        [FromRoute] Guid organizationId,
        BusinessUnitConversionModel model)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        var result = await businessUnitConverter.InitiateConversion(
            organization,
            model.ProviderAdminEmail!);

        return result.Match(
            providerId => RedirectToAction("Edit", "Providers", new { id = providerId }),
            errors =>
            {
                PersistErrorMessages(errors);
                return RedirectToAction("Index", new { organizationId });
            });
    }

    [HttpPost("reset")]
    [RequirePermission(Permission.Org_Billing_ConvertToBusinessUnit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> ResetAsync(
        [FromRoute] Guid organizationId,
        BusinessUnitConversionModel model)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        await businessUnitConverter.ResetConversion(organization, model.ProviderAdminEmail!);

        PersistSuccessMessage("Business unit conversion was successfully reset.");

        return RedirectToAction("Index", new { organizationId });
    }

    [HttpPost("resend-invite")]
    [RequirePermission(Permission.Org_Billing_ConvertToBusinessUnit)]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> ResendInviteAsync(
        [FromRoute] Guid organizationId,
        BusinessUnitConversionModel model)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            throw new NotFoundException();
        }

        await businessUnitConverter.ResendConversionInvite(organization, model.ProviderAdminEmail!);

        PersistSuccessMessage($"Invite was successfully resent to {model.ProviderAdminEmail}.");

        return RedirectToAction("Index", new { organizationId });
    }

    private async Task<ProviderUser?> GetInvitedProviderAdminAsync(
        Organization organization)
    {
        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        if (provider is not
            {
                Type: ProviderType.BusinessUnit,
                Status: ProviderStatusType.Pending
            })
        {
            return null;
        }

        var providerUsers =
            await providerUserRepository.GetManyByProviderAsync(provider.Id, ProviderUserType.ProviderAdmin);

        if (providerUsers.Count != 1)
        {
            return null;
        }

        var providerUser = providerUsers.First();

        return providerUser is
        {
            Type: ProviderUserType.ProviderAdmin,
            Status: ProviderUserStatusType.Invited,
            UserId: not null
        } ? providerUser : null;
    }

    private const string _errors = "errors";
    private const string _success = "Success";

    private void PersistSuccessMessage(string message) => TempData[_success] = message;
    private void PersistErrorMessages(List<string> errors)
    {
        var input = string.Join("|", errors);
        TempData[_errors] = input;
    }
    private string? ReadSuccessMessage() => ReadTempData<string>(_success);
    private List<string>? ReadErrorMessages()
    {
        var output = ReadTempData<string>(_errors);
        return string.IsNullOrEmpty(output) ? null : output.Split('|').ToList();
    }

    private T? ReadTempData<T>(string key) => TempData.TryGetValue(key, out var obj) && obj is T value ? value : default;
}
