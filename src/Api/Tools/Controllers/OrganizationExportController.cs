﻿using Bit.Api.Tools.Authorization;
using Bit.Api.Tools.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("organizations/{organizationId}")]
[Authorize("Application")]
public class OrganizationExportController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly ICollectionService _collectionService;
    private readonly ICipherService _cipherService;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly ICollectionRepository _collectionRepository;

    public OrganizationExportController(
        ICurrentContext currentContext,
        ICipherService cipherService,
        ICollectionService collectionService,
        IUserService userService,
        GlobalSettings globalSettings,
        IFeatureService featureService,
        IAuthorizationService authorizationService,
        IOrganizationCiphersQuery organizationCiphersQuery,
        ICollectionRepository collectionRepository)
    {
        _currentContext = currentContext;
        _cipherService = cipherService;
        _collectionService = collectionService;
        _userService = userService;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _authorizationService = authorizationService;
        _organizationCiphersQuery = organizationCiphersQuery;
        _collectionRepository = collectionRepository;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(Guid organizationId)
    {
        var canExportAll = await _authorizationService.AuthorizeAsync(User, new OrganizationScope(organizationId),
            VaultExportOperations.ExportWholeVault);
        if (canExportAll.Succeeded)
        {
            var allOrganizationCiphers = await _organizationCiphersQuery.GetAllOrganizationCiphers(organizationId);
            var allCollections = await _collectionRepository.GetManyByOrganizationIdAsync(organizationId);
            return Ok(new OrganizationExportResponseModel(allOrganizationCiphers, allCollections, _globalSettings));
        }

        var canExportManaged = await _authorizationService.AuthorizeAsync(User, new OrganizationScope(organizationId),
            VaultExportOperations.ExportManagedCollections);
        if (canExportManaged.Succeeded)
        {
            var userId = _userService.GetProperUserId(User)!.Value;

            var allUserCollections = await _collectionRepository.GetManyByUserIdAsync(userId);
            var managedOrgCollections = allUserCollections.Where(c => c.OrganizationId == organizationId && c.Manage).ToList();
            var managedCiphers =
                await _organizationCiphersQuery.GetOrganizationCiphersByCollectionIds(organizationId, managedOrgCollections.Select(c => c.Id));

            return Ok(new OrganizationExportResponseModel(managedCiphers, managedOrgCollections, _globalSettings));
        }

        // Unauthorized
        throw new NotFoundException();
    }
}
