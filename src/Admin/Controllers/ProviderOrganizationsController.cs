using Bit.Admin.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

public class ProviderOrganizationsController : Controller
{
    private readonly IProviderService _providerService;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;

    public ProviderOrganizationsController(IProviderService providerService,
        IProviderOrganizationRepository providerOrganizationRepository)
    {
        _providerService = providerService;
        _providerOrganizationRepository = providerOrganizationRepository;
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
        var unassignedOrganizations = await _providerOrganizationRepository.SearchAsync(name, ownerEmail, skip, count);
        var viewModel = new ProviderOrganizationViewModel
        {
            ProviderId = providerId,
            OrganizationName = string.IsNullOrWhiteSpace(name) ? null : name,
            OrganizationOwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail,
            Page = page,
            Count = count,
            Items = unassignedOrganizations.Select(uo => new ProviderOrganizationItemViewModel
            {
                OrganizationId = uo.OrganizationId,
                Name = uo.Name,
                PlanType = uo.PlanType,
                OwnerEmail = uo.OwnerEmail
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddExisting(ProviderOrganizationViewModel model)
    {
        return RedirectToAction("Edit", "Providers", new { id = model.ProviderId });
    }
}
