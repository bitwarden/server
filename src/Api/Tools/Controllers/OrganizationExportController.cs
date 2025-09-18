using Bit.Api.Tools.Authorization;
using Bit.Api.Tools.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("organizations/{organizationId}")]
[Authorize("Application")]
public class OrganizationExportController : Controller
{
    private readonly IUserService _userService;
    private readonly GlobalSettings _globalSettings;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IFeatureService _featureService;

    public OrganizationExportController(
        IUserService userService,
        GlobalSettings globalSettings,
        IAuthorizationService authorizationService,
        IOrganizationCiphersQuery organizationCiphersQuery,
        ICollectionRepository collectionRepository,
        IFeatureService featureService)
    {
        _userService = userService;
        _globalSettings = globalSettings;
        _authorizationService = authorizationService;
        _organizationCiphersQuery = organizationCiphersQuery;
        _collectionRepository = collectionRepository;
        _featureService = featureService;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(Guid organizationId)
    {
        var canExportAll = await _authorizationService.AuthorizeAsync(User, new OrganizationScope(organizationId),
            VaultExportOperations.ExportWholeVault);
        if (canExportAll.Succeeded)
        {
            var allOrganizationCiphers =
                _featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                    ? await _organizationCiphersQuery.GetAllOrganizationCiphersExcludingDefaultUserCollections(
                        organizationId)
                    : await _organizationCiphersQuery.GetAllOrganizationCiphers(organizationId);

            var allCollections =
                await _collectionRepository.GetManySharedCollectionsByOrganizationIdAsync(organizationId);

            return Ok(new OrganizationExportResponseModel(allOrganizationCiphers, allCollections, _globalSettings));
        }

        var canExportManaged = await _authorizationService.AuthorizeAsync(User, new OrganizationScope(organizationId),
            VaultExportOperations.ExportManagedCollections);
        if (canExportManaged.Succeeded)
        {
            var userId = _userService.GetProperUserId(User)!.Value;

            var allUserCollections = await _collectionRepository.GetManyByUserIdAsync(userId);
            var managedOrgCollections =
                allUserCollections.Where(c => c.OrganizationId == organizationId && c.Manage).ToList();
            var filteredCollections = _featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                ? managedOrgCollections.Where(c => c.Type != CollectionType.DefaultUserCollection)
                : managedOrgCollections;

            var managedCiphers =
                _featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
                    ? await _organizationCiphersQuery.GetAllOrganizationCiphersExcludingDefaultUserCollections(
                        organizationId)
                    : await _organizationCiphersQuery.GetOrganizationCiphersByCollectionIds(organizationId,
                        filteredCollections.Select(c => c.Id));

            return Ok(new OrganizationExportResponseModel(managedCiphers, filteredCollections, _globalSettings));
        }

        // Unauthorized
        throw new NotFoundException();
    }
}
