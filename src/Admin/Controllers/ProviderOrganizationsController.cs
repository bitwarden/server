using Bit.Admin.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

public class ProviderOrganizationsController : Controller
{
    private readonly IProviderService _providerService;
    private readonly IOrganizationRepository _organizationRepository;

    public ProviderOrganizationsController(
        IProviderService providerService,
        IOrganizationRepository organizationRepository)
    {
        _providerService = providerService;
        _organizationRepository = organizationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> AddExisting(Guid providerId, string name = null, string ownerEmail = null, int page = 1, int count = 25)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (count < 1)
        {
            count = 1;
        }

        var skip = (page - 1) * count;
        var unassignedOrganizations = await _organizationRepository.SearchUnassignedToProviderAsync(name, ownerEmail, skip, count);
        var viewModel = new ProviderOrganizationViewModel
        {
            ProviderId = providerId,
            OrganizationName = string.IsNullOrWhiteSpace(name) ? null : name,
            OrganizationOwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail,
            Page = page,
            Count = count,
            Items = unassignedOrganizations.Select(uo => new ProviderOrganizationItemViewModel
            {
                Id = uo.Id,
                Name = uo.Name,
                PlanType = uo.PlanType
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddExisting(ProviderOrganizationViewModel model)
    {
        var organizationIds = model.Items.Where(o => o.Selected).Select(o => o.Id).ToArray();

        await _providerService.AddOrganizationsToReseller(model.ProviderId, organizationIds);

        return RedirectToAction("Edit", "Providers", new { id = model.ProviderId });
    }
}
