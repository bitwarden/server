using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
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
    private readonly IAuthorizationService _authorizationService;
    private readonly IFeatureService _featureService;

    public ImportCiphersController(
        ICollectionCipherRepository collectionCipherRepository,
        ICipherService cipherService,
        IUserService userService,
        ICurrentContext currentContext,
        ILogger<ImportCiphersController> logger,
        GlobalSettings globalSettings,
        IAuthorizationService authorizationService,
        IFeatureService featureService)
    {
        _cipherService = cipherService;
        _userService = userService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
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
        var authorized = FlexibleCollectionsIsEnabled
            ? (await _authorizationService.AuthorizeAsync(User, collections, CollectionOperations.Create)).Succeeded
            : !await _currentContext.AccessImportExport(orgId);

        if (!authorized)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
        await _cipherService.ImportCiphersAsync(collections, ciphers, model.CollectionRelationships, userId);
    }
}
