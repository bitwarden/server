using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.AdminConsole.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProviderOrganizationsController : Controller
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRemoveOrganizationFromProviderCommand _removeOrganizationFromProviderCommand;

    public ProviderOrganizationsController(IProviderRepository providerRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IOrganizationRepository organizationRepository,
        IRemoveOrganizationFromProviderCommand removeOrganizationFromProviderCommand)
    {
        _providerRepository = providerRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _organizationRepository = organizationRepository;
        _removeOrganizationFromProviderCommand = removeOrganizationFromProviderCommand;
    }

    [HttpPost]
    [RequirePermission(Permission.Provider_Edit)]
    public async Task<IActionResult> DeleteAsync(Guid providerId, Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return RedirectToAction("Index", "Providers");
        }

        var providerOrganization = await _providerOrganizationRepository.GetByIdAsync(id);
        if (providerOrganization is null)
        {
            return RedirectToAction("View", "Providers", new { id = providerId });
        }

        var organization = await _organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);
        if (organization == null)
        {
            return RedirectToAction("View", "Providers", new { id = providerId });
        }

        try
        {
            await _removeOrganizationFromProviderCommand.RemoveOrganizationFromProvider(
                provider,
                providerOrganization,
                organization);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(ex.Message);
        }

        return Json(null);
    }
}
