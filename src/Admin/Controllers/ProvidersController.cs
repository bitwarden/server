﻿using Bit.Admin.Enums;
using Bit.Admin.Models;
using Bit.Admin.Utilities;
using Bit.Core.Entities.Provider;
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
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IProviderService _providerService;

    public ProvidersController(IProviderRepository providerRepository, IProviderUserRepository providerUserRepository,
        IProviderOrganizationRepository providerOrganizationRepository, IProviderService providerService,
        GlobalSettings globalSettings, IApplicationCacheService applicationCacheService)
    {
        _providerRepository = providerRepository;
        _providerUserRepository = providerUserRepository;
        _providerOrganizationRepository = providerOrganizationRepository;
        _providerService = providerService;
        _globalSettings = globalSettings;
        _applicationCacheService = applicationCacheService;
    }

    [RequirePermission(Permission.Provider_List_View)]
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
            Action = "View",
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
    [RequirePermission(Permission.Provider_Create)]
    public async Task<IActionResult> Create(CreateProviderModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _providerService.CreateAsync(model.OwnerEmail);

        return RedirectToAction("Index");
    }

    [RequirePermission(Permission.Provider_View)]
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

    [RequirePermission(Permission.Provider_ResendEmailInvite)]
    public async Task<IActionResult> ResendInvite(Guid ownerId, Guid providerId)
    {
        await _providerService.ResendProviderSetupInviteEmailAsync(providerId, ownerId);
        TempData["InviteResentTo"] = ownerId;
        return RedirectToAction("Edit", new { id = providerId });
    }
}
