using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
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

        var userId = _userService.GetProperUserId(User) ?? throw new InvalidOperationException("User ID not found");
        var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
        var ciphers = model.Ciphers.Select(c => c.ToCipherDetails(userId, false)).ToList();
        await _importCiphersCommand.ImportIntoIndividualVaultAsync(folders, ciphers, model.FolderRelationships, userId);
    }

    [HttpPost("import-organization")]
    public async Task PostImportOrganization([FromQuery] string organizationId,
        [FromBody] ImportOrganizationCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Length > _globalSettings.ImportCiphersLimitation.CiphersLimit ||
             model.CollectionRelationships.Length > _globalSettings.ImportCiphersLimitation.CollectionRelationshipsLimit ||
             model.Collections.Length > _globalSettings.ImportCiphersLimitation.CollectionsLimit ||
             model.Folders.Length > _globalSettings.ImportCiphersLimitation.FoldersLimit ||
             model.FolderRelationships.Length > _globalSettings.ImportCiphersLimitation.FolderRelationshipsLimit))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgId = new Guid(organizationId);
        var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();

        var authorized = await CheckOrgImportPermissionAsync(collections, orgId);
        if (!authorized)
        {
            throw new BadRequestException("Not enough privileges to import into this organization.");
        }

        var userId = _userService.GetProperUserId(User) ?? throw new InvalidOperationException("User ID not found");
        var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
        var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
        await _importCiphersCommand.ImportIntoOrganizationalVaultAsync(collections, ciphers, model.CollectionRelationships, userId, folders, model.FolderRelationships);
    }

    private async Task<bool> CheckOrgImportPermissionAsync(List<Collection> collections, Guid orgId)
    {
        // If we're importing into the default collection then all we check
        // is whether the user has access to the import feature at all
        if (collections.Count == 0)
        {
            if (!await _currentContext.AccessImportExport(orgId))
            {
                return false;
            }
            return true;
        }

        // Calling Repository instead of Service as we want to get all the collections, regardless of permission
        // Permissions check will be done later on AuthorizationService
        var orgCollectionIds =
            (await _collectionRepository.GetManyByOrganizationIdAsync(orgId))
            .Select(c => c.Id)
            .ToHashSet();

        var existingCollections = collections.Where(tc => orgCollectionIds.Contains(tc.Id));
        var hasNewCollections = collections.Any(tc => !orgCollectionIds.Contains(tc.Id));

        if (hasNewCollections)
        {
            var canCreateNewCollections =
                (await _currentContext.AccessImportExport(orgId)) ||
                (await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Create)).Succeeded;
            if (!canCreateNewCollections)
            {
                return false;
            }
        }

        if (existingCollections.Any())
        {
            var canImportIntoExistingCollections = (await _authorizationService.AuthorizeAsync(User, existingCollections, BulkCollectionOperations.ImportCiphers)).Succeeded;
            if (!canImportIntoExistingCollections)
            {
                return false;
            }
        }

        return true;
    }
}
