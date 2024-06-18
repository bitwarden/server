using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class ImportCiphersController : Controller
{
    private readonly ICipherService _cipherService;
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<ImportCiphersController> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAuthorizationService _authorizationService;

    public ImportCiphersController(
        ICipherService cipherService,
        IUserService userService,
        ICurrentContext currentContext,
        ILogger<ImportCiphersController> logger,
        GlobalSettings globalSettings,
        ICollectionRepository collectionRepository,
        IAuthorizationService authorizationService,
        IOrganizationRepository organizationRepository)
    {
        _cipherService = cipherService;
        _userService = userService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
        _collectionRepository = collectionRepository;
        _authorizationService = authorizationService;
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
        await _cipherService.ImportCiphersAsync(folders, ciphers, model.FolderRelationships);
    }

    [HttpPost("import-organization")]
    public async Task PostImport([FromQuery] string organizationId,
        [FromBody] ImportOrganizationCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Count() > 7000 || model.CollectionRelationships.Count() > 14000 ||
                model.Collections.Count() > 2000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgId = new Guid(organizationId);
        var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();


        //An User is allowed to import if CanCreate Collections or has AccessToImportExport
        var authorized = await CheckOrgImportPermission(collections, orgId);

        if (!authorized)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
        await _cipherService.ImportCiphersAsync(collections, ciphers, model.CollectionRelationships, userId);
    }

    private async Task<bool> CheckOrgImportPermission(List<Collection> collections, Guid orgId)
    {
        //Users are allowed to import if they have the AccessToImportExport permission
        if (await _currentContext.AccessImportExport(orgId))
        {
            return true;
        }

        //Users allowed to import if they CanCreate Collections
        if (!(await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Create)).Succeeded)
        {
            return false;
        }

        //Calling Repository instead of Service as we want to get all the collections, regardless of permission
        //Permissions check will be done later on AuthorizationService
        var orgCollectionIds =
            (await _collectionRepository.GetManyByOrganizationIdAsync(orgId))
            .Select(c => c.Id)
            .ToHashSet();

        //We need to verify if the user is trying to import into existing collections
        var existingCollections = collections.Where(tc => orgCollectionIds.Contains(tc.Id));

        //When importing into existing collection, we need to verify if the user has permissions
        if (existingCollections.Any() && !(await _authorizationService.AuthorizeAsync(User, existingCollections, BulkCollectionOperations.ImportCiphers)).Succeeded)
        {
            return false;
        };

        return true;
    }
}
