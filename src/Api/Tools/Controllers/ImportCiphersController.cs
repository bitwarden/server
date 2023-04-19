using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Tools.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
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

    public ImportCiphersController(
        ICollectionCipherRepository collectionCipherRepository,
        ICipherService cipherService,
        IUserService userService,
        IProviderService providerService,
        ICurrentContext currentContext,
        ILogger<ImportCiphersController> logger,
        GlobalSettings globalSettings)
    {
        _cipherService = cipherService;
        _userService = userService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
    }

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
        if (!await _currentContext.AccessImportExport(orgId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();
        var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
        await _cipherService.ImportCiphersAsync(collections, ciphers, model.CollectionRelationships, userId);
    }
}
