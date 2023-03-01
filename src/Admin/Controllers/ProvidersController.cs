using Bit.Admin.Models;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Business;
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
    private readonly IOrganizationService _organizationService;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderService _providerService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IUserService _userService;
    private readonly ICreateProviderCommand _createProviderCommand;

    public ProvidersController(
        IOrganizationService organizationService,
        IProviderRepository providerRepository,
        IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository,
        IProviderService providerService,
        GlobalSettings globalSettings,
        IApplicationCacheService applicationCacheService,
        IReferenceEventService referenceEventService,
        IUserService userService,
        ICreateProviderCommand createProviderCommand)
    {
        _organizationService = organizationService;
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _applicationCacheService = applicationCacheService;
        _referenceEventService = referenceEventService;
        _userService = userService;
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
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is not { Type: ProviderType.Reseller })
        {
            return RedirectToAction("Index");
        }

        var organization = model.CreateOrganization(provider);
        await _organizationService.CreatePendingOrganization(organization, model.Owners);

        await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);
        await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.OrganizationCreatedByAdmin, organization)
        {
            EventRaisedByUser = _userService.GetUserName(User),
            SalesAssistedTrialStarted = model.SalesAssistedTrialStarted,
        });
        await _providerService.AddOrganization(providerId, organization.Id, null);

        return RedirectToAction("Edit", "Providers", new { id = providerId });
    }
}
