using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
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
    private readonly IFeatureService _featureService;

    public ImportCiphersController(
        ICipherService cipherService,
        IUserService userService,
        ICurrentContext currentContext,
        ILogger<ImportCiphersController> logger,
        GlobalSettings globalSettings,
        ICollectionRepository collectionRepository,
        IAuthorizationService authorizationService,
        IFeatureService featureService)
    {
        _cipherService = cipherService;
        _userService = userService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
        _collectionRepository = collectionRepository;
        _authorizationService = authorizationService;
        _featureService = featureService;
    }

    private bool FlexibleCollectionsIsEnabled => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    [HttpPost("import")]
    public async Task PostImport([FromBody] ImportCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Count() > 6000 || model.FolderRelationships.Count() > 6000 ||
                model.Folders.Count() > 1000))
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
            (model.Ciphers.Count() > 6000 || model.CollectionRelationships.Count() > 12000 ||
                model.Collections.Count() > 1000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgId = new Guid(organizationId);
        var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();


        //An User is allowed to import if CanCreate Collections or has AccessToImportExport
        var authorized = FlexibleCollectionsIsEnabled
            ? await CheckOrgImportPermission(collections, orgId) || await _currentContext.AccessImportExport(orgId)
            : await _currentContext.AccessImportExport(orgId);

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
        if (!(await _authorizationService.AuthorizeAsync(User, collections, BulkCollectionOperations.Create)).Succeeded)
        {
            return false;
        }

        var orgCollectionIds =
            (await _collectionRepository.GetManyByOrganizationIdAsync(orgId))
            .Select(c => c.Id)
            .ToHashSet();

        //We need to verify if the user is trying to import into existing collections
        var existingCollections = collections.Where(tc => orgCollectionIds.Contains(tc.Id));

        //When importing into existing collection, we need to verify if the user has permissions
        if (existingCollections.Count() > 0 && !(await _authorizationService.AuthorizeAsync(User, existingCollections, BulkCollectionOperations.ImportCiphers)).Succeeded)
        {
            return false;
        };

        return true;
    }
}
