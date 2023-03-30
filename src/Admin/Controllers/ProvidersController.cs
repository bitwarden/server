using Bit.Admin.Models;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Providers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class ProvidersController : Controller
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderService _providerService;
    private readonly ICreateProviderCommand _createProviderCommand;

    public ProvidersController(
        IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderService providerService,
        GlobalSettings globalSettings,
        IApplicationCacheService applicationCacheService,
        ICreateProviderCommand createProviderCommand)
    {
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _applicationCacheService = applicationCacheService;
        _createProviderCommand = createProviderCommand;
    }

    public async Task<IActionResult> Index(string name = null, string userEmail = null, int page = 1, int count = 25)
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
        var providers = await _providerRepository.SearchAsync(name, userEmail, skip, count);
        return View(new ProvidersModel
        {
            Items = providers as List<Provider>,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail,
            Page = page,
            Count = count,
            Action = _globalSettings.SelfHosted ? "View" : "Edit",
            SelfHosted = _globalSettings.SelfHosted
        });
    }

    public IActionResult Create(string ownerEmail = null)
    {
        return View(new CreateProviderModel
        {
            OwnerEmail = ownerEmail
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var provider = model.ToProvider();
        switch (provider.Type)
        {
            case ProviderType.Msp:
                await _createProviderCommand.CreateMspAsync(provider, model.OwnerEmail);
                break;
            case ProviderType.Reseller:
                await _createProviderCommand.CreateResellerAsync(provider);
                break;
        }

        return RedirectToAction("Edit", new { id = provider.Id });
    }

    public async Task<IActionResult> View(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        var users = await _providerUserRepository.GetManyDetailsByProviderAsync(id);
        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);
        return View(new ProviderViewModel(provider, users, providerOrganizations));
    }

    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        var users = await _providerUserRepository.GetManyDetailsByProviderAsync(id);
        var providerOrganizations = await _providerOrganizationRepository.GetManyDetailsByProviderAsync(id);
        return View(new ProviderEditModel(provider, users, providerOrganizations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IActionResult> Edit(Guid id, ProviderEditModel model)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null)
        {
            return RedirectToAction("Index");
        }

        model.ToProvider(provider);
        await _providerRepository.ReplaceAsync(provider);
        await _applicationCacheService.UpsertProviderAbilityAsync(provider);
        return RedirectToAction("Edit", new { id });
    }

    public async Task<IActionResult> ResendInvite(Guid ownerId, Guid providerId)
    {
        await _providerService.ResendProviderSetupInviteEmailAsync(providerId, ownerId);
        TempData["InviteResentTo"] = ownerId;
        return RedirectToAction("Edit", new { id = providerId });
    }

    [HttpGet]
    public async Task<IActionResult> AddExistingOrganization(Guid id, string name = null, string ownerEmail = null, int page = 1, int count = 25)
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
        var viewModel = new OrganizationUnassignedToProviderSearchViewModel
        {
            OrganizationName = string.IsNullOrWhiteSpace(name) ? null : name,
            OrganizationOwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail,
            Page = page,
            Count = count,
            Items = unassignedOrganizations.Select(uo => new OrganizationSelectableViewModel
            {
                Id = uo.Id,
                Name = uo.Name,
                PlanType = uo.PlanType
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddExistingOrganization(Guid id, OrganizationUnassignedToProviderSearchViewModel model)
    {
        var organizationIds = model.Items.Where(o => o.Selected).Select(o => o.Id).ToArray();
        if (organizationIds.Any())
        {
            await _providerService.AddOrganizationsToReseller(id, organizationIds);
        }

        return RedirectToAction("Edit", "Providers", new { id = id });
    }

    [HttpGet]
    public async Task<IActionResult> CreateOrganization(Guid providerId)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is not { Type: ProviderType.Reseller })
        {
            return RedirectToAction("Index");
        }

        return View(new OrganizationEditModel(provider));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(Guid providerId, OrganizationEditModel model)
    {
        // TODO : Insert logic to create the new Organization entry, create an OrganizationUser entry for the owner and send the invitation email

        return RedirectToAction("Edit", "Providers", new { id = providerId });
    }
}
