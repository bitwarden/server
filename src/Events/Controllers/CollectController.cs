﻿using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Events.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Events.Controllers;

[Route("collect")]
[Authorize("Application")]
public class CollectController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IEventService _eventService;
    private readonly ICipherRepository _cipherRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IFeatureService _featureService;
    private readonly IApplicationCacheService _applicationCacheService;

    public CollectController(
        ICurrentContext currentContext,
        IEventService eventService,
        ICipherRepository cipherRepository,
        IOrganizationRepository organizationRepository,
        IFeatureService featureService,
        IApplicationCacheService applicationCacheService)
    {
        _currentContext = currentContext;
        _eventService = eventService;
        _cipherRepository = cipherRepository;
        _organizationRepository = organizationRepository;
        _featureService = featureService;
        _applicationCacheService = applicationCacheService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] IEnumerable<EventModel> model)
    {
        if (model == null || !model.Any())
        {
            return new BadRequestResult();
        }
        var cipherEvents = new List<Tuple<Cipher, EventType, DateTime?>>();
        var ciphersCache = new Dictionary<Guid, Cipher>();
        foreach (var eventModel in model)
        {
            switch (eventModel.Type)
            {
                // User events
                case EventType.User_ClientExportedVault:
                    await _eventService.LogUserEventAsync(_currentContext.UserId.Value, eventModel.Type, eventModel.Date);
                    break;
                // Cipher events
                case EventType.Cipher_ClientAutofilled:
                case EventType.Cipher_ClientCopiedHiddenField:
                case EventType.Cipher_ClientCopiedPassword:
                case EventType.Cipher_ClientCopiedCardCode:
                case EventType.Cipher_ClientToggledCardNumberVisible:
                case EventType.Cipher_ClientToggledCardCodeVisible:
                case EventType.Cipher_ClientToggledHiddenFieldVisible:
                case EventType.Cipher_ClientToggledPasswordVisible:
                case EventType.Cipher_ClientViewed:
                    if (!eventModel.CipherId.HasValue)
                    {
                        continue;
                    }
                    Cipher cipher = null;
                    if (ciphersCache.ContainsKey(eventModel.CipherId.Value))
                    {
                        cipher = ciphersCache[eventModel.CipherId.Value];
                    }
                    else
                    {
                        cipher = await _cipherRepository.GetByIdAsync(eventModel.CipherId.Value,
                           _currentContext.UserId.Value);
                    }
                    if (cipher == null)
                    {
                        // When the user cannot access the cipher directly, check if the organization allows for
                        // admin/owners access to all collections and the user can access the cipher from that perspective.
                        if (!eventModel.OrganizationId.HasValue)
                        {
                            continue;
                        }

                        cipher = await _cipherRepository.GetByIdAsync(eventModel.CipherId.Value);
                        var cipherBelongsToOrg = cipher.OrganizationId == eventModel.OrganizationId;
                        var org = _currentContext.GetOrganization(eventModel.OrganizationId.Value);

                        if (!cipherBelongsToOrg || org == null || cipher == null)
                        {
                            continue;
                        }
                    }
                    if (!ciphersCache.ContainsKey(eventModel.CipherId.Value))
                    {
                        ciphersCache.Add(eventModel.CipherId.Value, cipher);
                    }
                    cipherEvents.Add(new Tuple<Cipher, EventType, DateTime?>(cipher, eventModel.Type, eventModel.Date));
                    break;
                case EventType.Organization_ClientExportedVault:
                    if (!eventModel.OrganizationId.HasValue)
                    {
                        continue;
                    }
                    var organization = await _organizationRepository.GetByIdAsync(eventModel.OrganizationId.Value);
                    await _eventService.LogOrganizationEventAsync(organization, eventModel.Type, eventModel.Date);
                    break;
                default:
                    continue;
            }
        }
        if (cipherEvents.Any())
        {
            foreach (var eventsBatch in cipherEvents.Chunk(50))
            {
                await _eventService.LogCipherEventsAsync(eventsBatch);
            }
        }
        return new OkResult();
    }
}
