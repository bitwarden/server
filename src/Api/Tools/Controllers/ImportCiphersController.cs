// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.ImportFeatures.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class ImportCiphersController : Controller
{
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<ImportCiphersController> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly IImportCiphersCommand _importCiphersCommand;

    public ImportCiphersController(
        IUserService userService,
        ICurrentContext currentContext,
        ILogger<ImportCiphersController> logger,
        GlobalSettings globalSettings,
        ICollectionRepository collectionRepository,
        IAuthorizationService authorizationService,
        IImportCiphersCommand importCiphersCommand)
    {
        _userService = userService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
        _collectionRepository = collectionRepository;
        _authorizationService = authorizationService;
        _importCiphersCommand = importCiphersCommand;
    }

    [HttpPost("import")]
    public async Task PostImport([FromBody] ImportCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Count() > 7000 || model.FolderRelationships.Count() > 7000 ||
                model.Folders.Count() > 2000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
        var ciphers = model.Ciphers.Select(c => c.ToCipherDetails(userId, false)).ToList();
        await _importCiphersCommand.ImportIntoIndividualVaultAsync(folders, ciphers, model.FolderRelationships, userId);
    }

    [HttpPost("import-organization")]
    public async Task PostImportOrganization([FromQuery] string organizationId,
        [FromBody] ImportOrganizationCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Count() > _globalSettings.ImportCiphersLimitation.CiphersLimit ||
             model.CollectionRelationships.Count() > _globalSettings.ImportCiphersLimitation.CollectionRelationshipsLimit ||
             model.Collections.Count() > _globalSettings.ImportCiphersLimitation.CollectionsLimit))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgId = new Guid(organizationId);
        var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();


        //An User is allowed to import if CanCreate Collections or has AccessToImportExport
        var authorized = await CheckOrgImportPermission(collections, orgId);
        if (!authorized)
        {
            throw new BadRequestException("Not enough privileges to import into this organization.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
        await _importCiphersCommand.ImportIntoOrganizationalVaultAsync(collections, ciphers, model.CollectionRelationships, userId);
    }

    private async Task<bool> CheckOrgImportPermission(List<Collection> collections, Guid orgId)
    {
        //Users are allowed to import if they have the AccessToImportExport permission
        if (await _currentContext.AccessImportExport(orgId))
        {
            return true;
        }

        //Calling Repository instead of Service as we want to get all the collections, regardless of permission
        //Permissions check will be done later on AuthorizationService
        var orgCollectionIds =
            (await _collectionRepository.GetManyByOrganizationIdAsync(orgId))
            .Select(c => c.Id)
            .ToHashSet();

        // when there are no collections, then we can import
        if (collections.Count == 0)
        {
            return true;
        }

        // are we trying to import into existing collections?
        var existingCollections = collections.Where(tc => orgCollectionIds.Contains(tc.Id));

        // are we trying to create new collections?
        var hasNewCollections = collections.Any(tc => !orgCollectionIds.Contains(tc.Id));

        // suppose we have both new and existing collections
        if (hasNewCollections && existingCollections.Any())
        {
            // since we are creating new collection, user must have import/manage and create collection permission
            if ((await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Create)).Succeeded
                && (await _authorizationService.AuthorizeAsync(User, existingCollections, BulkCollectionOperations.ImportCiphers)).Succeeded)
            {
                // can import collections and create new ones
                return true;
            }
            else
            {
                // user does not have permission to import
                return false;
            }
        }

        // suppose we have new collections and none of our collections exist
        if (hasNewCollections && !existingCollections.Any())
        {
            // user is trying to create new collections
            // we need to check if the user has permission to create collections
            if ((await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Create)).Succeeded)
            {
                return true;
            }
            else
            {
                // user does not have permission to create new collections
                return false;
            }
        }

        // in many import formats, we don't create collections, we just import ciphers into an existing collection

        // When importing, we need to verify if the user has ImportCiphers permission
        if (existingCollections.Any() && (await _authorizationService.AuthorizeAsync(User, existingCollections, BulkCollectionOperations.ImportCiphers)).Succeeded)
        {
            return true;
        };

        return false;
    }
}
