using System.Text.Json;
using Azure.Messaging.EventGrid;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class CiphersController : Controller
{
    private static readonly Version _fido2KeyCipherMinimumVersion = new Version(Constants.Fido2KeyCipherMinimumVersion);

    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICipherService _cipherService;
    private readonly IUserService _userService;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly IProviderService _providerService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<CiphersController> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICollectionRepository _collectionRepository;

    public CiphersController(
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICipherService cipherService,
        IUserService userService,
        IAttachmentStorageService attachmentStorageService,
        IProviderService providerService,
        ICurrentContext currentContext,
        ILogger<CiphersController> logger,
        GlobalSettings globalSettings,
        IFeatureService featureService,
        IOrganizationCiphersQuery organizationCiphersQuery,
        IApplicationCacheService applicationCacheService,
        ICollectionRepository collectionRepository)
    {
        _cipherRepository = cipherRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _cipherService = cipherService;
        _userService = userService;
        _attachmentStorageService = attachmentStorageService;
        _providerService = providerService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _organizationCiphersQuery = organizationCiphersQuery;
        _applicationCacheService = applicationCacheService;
        _collectionRepository = collectionRepository;
    }

    [HttpGet("{id}")]
    public async Task<CipherResponseModel> Get(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        return new CipherResponseModel(cipher, _globalSettings);
    }

    [HttpGet("{id}/admin")]
    public async Task<CipherMiniResponseModel> GetAdmin(string id)
    {
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.ViewAllCollections(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(cipher.OrganizationId.Value);
        var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

        return new CipherMiniDetailsResponseModel(cipher, _globalSettings, collectionCiphersGroupDict, cipher.OrganizationUseTotp);
    }

    [HttpGet("{id}/full-details")]
    [HttpGet("{id}/details")]
    public async Task<CipherDetailsResponseModel> GetDetails(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id);
        return new CipherDetailsResponseModel(cipher, _globalSettings, collectionCiphers);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<CipherDetailsResponseModel>> Get()
    {
        var userId = _userService.GetProperUserId(User).Value;
        var hasOrgs = _currentContext.Organizations?.Any() ?? false;
        // TODO: Use hasOrgs proper for cipher listing here?
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, withOrganizations: true || hasOrgs);
        Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
        if (hasOrgs)
        {
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
        }

        var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, _globalSettings,
            collectionCiphersGroupDict)).ToList();
        return new ListResponseModel<CipherDetailsResponseModel>(responses);
    }

    [HttpPost("")]
    public async Task<CipherResponseModel> Post([FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = model.ToCipherDetails(userId);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, userId, model.LastKnownRevisionDate, null, cipher.OrganizationId.HasValue);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPost("create")]
    public async Task<CipherResponseModel> PostCreate([FromBody] CipherCreateRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = model.Cipher.ToCipherDetails(userId);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, userId, model.Cipher.LastKnownRevisionDate, model.CollectionIds, cipher.OrganizationId.HasValue);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPost("admin")]
    public async Task<CipherMiniResponseModel> PostAdmin([FromBody] CipherCreateRequestModel model)
    {
        var cipher = model.Cipher.ToOrganizationCipher();
        // Only users that can edit all ciphers can create new ciphers via the admin endpoint
        // Other users should use the regular POST/create endpoint
        if (!await CanEditAllCiphersAsync(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SaveAsync(cipher, userId, model.Cipher.LastKnownRevisionDate, model.CollectionIds, true, false);

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);
        return response;
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<CipherResponseModel> Put(Guid id, [FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        ValidateClientVersionForFido2CredentialSupport(cipher);

        var collectionIds = (await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id)).Select(c => c.CollectionId).ToList();
        var modelOrgId = string.IsNullOrWhiteSpace(model.OrganizationId) ?
            (Guid?)null : new Guid(model.OrganizationId);
        if (cipher.OrganizationId != modelOrgId)
        {
            throw new BadRequestException("Organization mismatch. Re-sync if you recently moved this item, " +
                "then try again.");
        }

        await _cipherService.SaveDetailsAsync(model.ToCipherDetails(cipher), userId, model.LastKnownRevisionDate, collectionIds);

        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/admin")]
    [HttpPost("{id}/admin")]
    public async Task<CipherMiniResponseModel> PutAdmin(Guid id, [FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(id);

        ValidateClientVersionForFido2CredentialSupport(cipher);

        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        var collectionIds = (await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id)).Select(c => c.CollectionId).ToList();
        // object cannot be a descendant of CipherDetails, so let's clone it.
        var cipherClone = model.ToCipher(cipher).Clone();
        await _cipherService.SaveAsync(cipherClone, userId, model.LastKnownRevisionDate, collectionIds, true, false);

        var response = new CipherMiniResponseModel(cipherClone, _globalSettings, cipher.OrganizationUseTotp);
        return response;
    }

    [HttpGet("organization-details")]
    public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCiphers(Guid organizationId)
    {
        if (!await CanAccessAllCiphersAsync(organizationId))
        {
            throw new NotFoundException();
        }

        var allOrganizationCiphers = await _organizationCiphersQuery.GetAllOrganizationCiphers(organizationId);

        var allOrganizationCipherResponses =
            allOrganizationCiphers.Select(c =>
                new CipherMiniDetailsResponseModel(c, _globalSettings, c.OrganizationUseTotp)
            );

        return new ListResponseModel<CipherMiniDetailsResponseModel>(allOrganizationCipherResponses);
    }

    [HttpGet("organization-details/assigned")]
    public async Task<ListResponseModel<CipherDetailsResponseModel>> GetAssignedOrganizationCiphers(Guid organizationId)
    {
        if (!await CanAccessOrganizationCiphersAsync(organizationId) || !_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var ciphers = await _organizationCiphersQuery.GetOrganizationCiphersForUser(organizationId, _currentContext.UserId.Value);

        if (await CanAccessUnassignedCiphersAsync(organizationId))
        {
            var unassignedCiphers = await _organizationCiphersQuery.GetUnassignedOrganizationCiphers(organizationId);
            ciphers = ciphers.Concat(unassignedCiphers.Select(c => new CipherDetailsWithCollections(c, null)
            {
                // Users that can access unassigned ciphers can also edit them
                Edit = true,
                ViewPassword = true,
            }));
        }

        var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, _globalSettings));

        return new ListResponseModel<CipherDetailsResponseModel>(responses);
    }

    /// <summary>
    /// Permission helper to determine if the current user can use the "/admin" variants of the cipher endpoints.
    /// Allowed for custom users with EditAnyCollection, providers, unrestricted owners and admins (allowAdminAccess setting is ON).
    /// Falls back to original EditAnyCollection permission check for when V1 flag is disabled.
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanEditCipherAsAdminAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // If we're not an "admin", we don't need to check the ciphers
        if (org is not ({ Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or { Permissions.EditAnyCollection: true }))
        {
            // Are we a provider user? If so, we need to be sure we're not restricted
            // Once the feature flag is removed, this check can be combined with the above
            if (await _currentContext.ProviderUserForOrgAsync(organizationId))
            {
                // Provider is restricted from editing ciphers, so we're not an "admin"
                if (_featureService.IsEnabled(FeatureFlagKeys.RestrictProviderAccess))
                {
                    return false;
                }

                // Provider is unrestricted, so we're an "admin", don't return early
            }
            else
            {
                // Not a provider or admin
                return false;
            }
        }

        // We know we're an "admin", now check the ciphers explicitly (in case admins are restricted)
        return await CanEditCiphersAsync(organizationId, cipherIds);
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanAccessAllCiphersAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // We do NOT need to check the organization collection management setting here because Owners/Admins can
        // ALWAYS access all ciphers in order to export them. Additionally, custom users with AccessImportExport,
        // EditAnyCollection, or AccessReports permissions can also always access all ciphers.
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.AccessImportExport: true } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.AccessReports: true })
        {
            return true;
        }

        // Provider users can access all ciphers.
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanEditAllCiphersAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // Custom users with EditAnyCollection permissions can always edit all ciphers
        if (org is { Type: OrganizationUserType.Custom, Permissions.EditAnyCollection: true })
        {
            return true;
        }

        var orgAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);

        // Owners/Admins can only edit all ciphers if the organization has the setting enabled
        if (orgAbility is { AllowAdminAccessToAllCollectionItems: true } && org is
            { Type: OrganizationUserType.Admin or OrganizationUserType.Owner })
        {
            return true;
        }

        // Provider users can edit all ciphers if RestrictProviderAccess is disabled
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return !_featureService.IsEnabled(FeatureFlagKeys.RestrictProviderAccess);
        }

        return false;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanAccessOrganizationCiphersAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // The user has a relationship with the organization;
        // they can access its ciphers in collections they've been assigned
        if (org is not null)
        {
            return true;
        }

        // Provider users can only access organization ciphers if RestrictProviderAccess is disabled
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return !_featureService.IsEnabled(FeatureFlagKeys.RestrictProviderAccess);
        }

        return false;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanAccessUnassignedCiphersAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);

        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        // Provider users can only access all ciphers if RestrictProviderAccess is disabled
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return !_featureService.IsEnabled(FeatureFlagKeys.RestrictProviderAccess);
        }

        return false;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanEditCiphersAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        // If the user can edit all ciphers for the organization, just check they all belong to the org
        if (await CanEditAllCiphersAsync(organizationId))
        {
            // TODO: This can likely be optimized to only query the requested ciphers and then checking they belong to the org
            var orgCiphers = (await _cipherRepository.GetManyByOrganizationIdAsync(organizationId)).ToDictionary(c => c.Id);

            // Ensure all requested ciphers are in orgCiphers
            if (cipherIds.Any(c => !orgCiphers.ContainsKey(c)))
            {
                return false;
            }

            return true;
        }

        // The user cannot access any ciphers for the organization, we're done
        if (!await CanAccessOrganizationCiphersAsync(organizationId))
        {
            return false;
        }

        var userId = _userService.GetProperUserId(User).Value;
        // Select all editable ciphers for this user belonging to the organization
        var editableOrgCipherList = (await _cipherRepository.GetManyByUserIdAsync(userId, true))
            .Where(c => c.OrganizationId == organizationId && c.UserId == null && c.Edit).ToList();

        // Special case for unassigned ciphers
        if (await CanAccessUnassignedCiphersAsync(organizationId))
        {
            var unassignedCiphers =
                (await _cipherRepository.GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(
                    organizationId));

            // Users that can access unassigned ciphers can also edit them
            editableOrgCipherList.AddRange(unassignedCiphers.Select(c => new CipherDetails(c) { Edit = true }));
        }

        var editableOrgCiphers = editableOrgCipherList
            .ToDictionary(c => c.Id);

        if (cipherIds.Any(c => !editableOrgCiphers.ContainsKey(c)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// This likely belongs to the BulkCollectionAuthorizationHandler
    /// </summary>
    private async Task<bool> CanEditItemsInCollections(Guid organizationId, IEnumerable<Guid> collectionIds)
    {
        if (await CanEditAllCiphersAsync(organizationId))
        {
            // TODO: This can likely be optimized to only query the requested ciphers and then checking they belong to the org
            var orgCollections = (await _collectionRepository.GetManyByOrganizationIdAsync(organizationId)).ToDictionary(c => c.Id);

            // Ensure all requested collections are in orgCollections
            if (collectionIds.Any(c => !orgCollections.ContainsKey(c)))
            {
                return false;
            }

            return true;
        }

        if (!await CanAccessOrganizationCiphersAsync(organizationId))
        {
            return false;
        }

        var userId = _userService.GetProperUserId(User).Value;
        var editableCollections = (await _collectionRepository.GetManyByUserIdAsync(userId))
            .Where(c => c.OrganizationId == organizationId && !c.ReadOnly)
            .ToDictionary(c => c.Id);

        if (collectionIds.Any(c => !editableCollections.ContainsKey(c)))
        {
            return false;
        }

        return true;
    }

    [HttpPut("{id}/partial")]
    [HttpPost("{id}/partial")]
    public async Task<CipherResponseModel> PutPartial(Guid id, [FromBody] CipherPartialRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folderId = string.IsNullOrWhiteSpace(model.FolderId) ? null : (Guid?)new Guid(model.FolderId);
        await _cipherRepository.UpdatePartialAsync(id, userId, folderId, model.Favorite);

        var cipher = await GetByIdAsync(id, userId);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/share")]
    [HttpPost("{id}/share")]
    public async Task<CipherResponseModel> PutShare(Guid id, [FromBody] CipherShareRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(id);
        if (cipher == null || cipher.UserId != userId ||
            !await _currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
        {
            throw new NotFoundException();
        }

        ValidateClientVersionForFido2CredentialSupport(cipher);

        var original = cipher.Clone();
        await _cipherService.ShareAsync(original, model.Cipher.ToCipher(cipher), new Guid(model.Cipher.OrganizationId),
            model.CollectionIds.Select(c => new Guid(c)), userId, model.Cipher.LastKnownRevisionDate);

        var sharedCipher = await GetByIdAsync(id, userId);
        var response = new CipherResponseModel(sharedCipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/collections")]
    [HttpPost("{id}/collections")]
    public async Task<CipherDetailsResponseModel> PutCollections(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher,
            model.CollectionIds.Select(c => new Guid(c)), userId, false);

        var updatedCipher = await GetByIdAsync(id, userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id);

        return new CipherDetailsResponseModel(updatedCipher, _globalSettings, collectionCiphers);
    }

    [HttpPut("{id}/collections_v2")]
    [HttpPost("{id}/collections_v2")]
    public async Task<OptionalCipherDetailsResponseModel> PutCollections_vNext(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher,
            model.CollectionIds.Select(c => new Guid(c)), userId, false);

        var updatedCipher = await GetByIdAsync(id, userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id);
        // If a user removes the last Can Manage access of a cipher, the "updatedCipher" will return null
        // We will be returning an "Unavailable" property so the client knows the user can no longer access this
        var response = new OptionalCipherDetailsResponseModel()
        {
            Unavailable = updatedCipher is null,
            Cipher = updatedCipher is null
                ? null
                : new CipherDetailsResponseModel(updatedCipher, _globalSettings, collectionCiphers)
        };
        return response;
    }

    [HttpPut("{id}/collections-admin")]
    [HttpPost("{id}/collections-admin")]
    public async Task<CipherMiniDetailsResponseModel> PutCollectionsAdmin(string id, [FromBody] CipherCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(new Guid(id));

        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        var collectionIds = model.CollectionIds.Select(c => new Guid(c)).ToList();

        // In V1, we still need to check if the user can edit the collections they're submitting
        // This should only happen for unassigned ciphers (otherwise restricted admins would use the normal collections endpoint)
        if (!await CanEditItemsInCollections(cipher.OrganizationId.Value, collectionIds))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher, collectionIds, userId, true);

        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(cipher.OrganizationId.Value);
        var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

        return new CipherMiniDetailsResponseModel(cipher, _globalSettings, collectionCiphersGroupDict, cipher.OrganizationUseTotp);
    }

    [HttpPost("bulk-collections")]
    public async Task PostBulkCollections([FromBody] CipherBulkUpdateCollectionsRequestModel model)
    {
        if (!await CanEditCiphersAsync(model.OrganizationId, model.CipherIds) ||
            !await CanEditItemsInCollections(model.OrganizationId, model.CollectionIds))
        {
            throw new NotFoundException();
        }

        if (model.RemoveCollections)
        {
            await _collectionCipherRepository.RemoveCollectionsForManyCiphersAsync(model.OrganizationId, model.CipherIds, model.CollectionIds);
        }
        else
        {
            await _collectionCipherRepository.AddCollectionsForManyCiphersAsync(model.OrganizationId, model.CipherIds, model.CollectionIds);
        }
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAsync(cipher, userId);
    }

    [HttpDelete("{id}/admin")]
    [HttpPost("{id}/delete-admin")]
    public async Task DeleteAdmin(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAsync(cipher, userId, true);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task DeleteMany([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time. " +
                "Consider using the \"Purge Vault\" option instead.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.DeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
    }

    [HttpDelete("admin")]
    [HttpPost("delete-admin")]
    public async Task DeleteManyAdmin([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time. " +
                "Consider using the \"Purge Vault\" option instead.");
        }

        if (model == null)
        {
            throw new NotFoundException();
        }

        var cipherIds = model.Ids.Select(i => new Guid(i)).ToList();

        if (string.IsNullOrWhiteSpace(model.OrganizationId) ||
            !await CanEditCipherAsAdminAsync(new Guid(model.OrganizationId), cipherIds))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.DeleteManyAsync(cipherIds, userId, new Guid(model.OrganizationId), true);
    }

    [HttpPut("{id}/delete")]
    public async Task PutDelete(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }
        await _cipherService.SoftDeleteAsync(cipher, userId);
    }

    [HttpPut("{id}/delete-admin")]
    public async Task PutDeleteAdmin(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.SoftDeleteAsync(cipher, userId, true);
    }

    [HttpPut("delete")]
    public async Task PutDeleteMany([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SoftDeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
    }

    [HttpPut("delete-admin")]
    public async Task PutDeleteManyAdmin([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time.");
        }

        if (model == null)
        {
            throw new NotFoundException();
        }

        var cipherIds = model.Ids.Select(i => new Guid(i)).ToList();

        if (string.IsNullOrWhiteSpace(model.OrganizationId) ||
            !await CanEditCipherAsAdminAsync(new Guid(model.OrganizationId), cipherIds))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SoftDeleteManyAsync(cipherIds, userId, new Guid(model.OrganizationId), true);
    }

    [HttpPut("{id}/restore")]
    public async Task<CipherResponseModel> PutRestore(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(cipher, userId);
        return new CipherResponseModel(cipher, _globalSettings);
    }

    [HttpPut("{id}/restore-admin")]
    public async Task<CipherMiniResponseModel> PutRestoreAdmin(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(cipher, userId, true);
        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpPut("restore")]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PutRestoreMany([FromBody] CipherBulkRestoreRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only restore up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var cipherIdsToRestore = new HashSet<Guid>(model.Ids.Select(i => new Guid(i)));

        var restoredCiphers = await _cipherService.RestoreManyAsync(cipherIdsToRestore, userId);
        var responses = restoredCiphers.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));
        return new ListResponseModel<CipherMiniResponseModel>(responses);
    }

    [HttpPut("restore-admin")]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PutRestoreManyAdmin([FromBody] CipherBulkRestoreRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only restore up to 500 items at a time.");
        }

        if (model == null)
        {
            throw new NotFoundException();
        }

        var cipherIdsToRestore = new HashSet<Guid>(model.Ids.Select(i => new Guid(i)));

        if (model.OrganizationId == default || !await CanEditCipherAsAdminAsync(model.OrganizationId, cipherIdsToRestore))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;

        var restoredCiphers = await _cipherService.RestoreManyAsync(cipherIdsToRestore, userId, model.OrganizationId, true);
        var responses = restoredCiphers.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));
        return new ListResponseModel<CipherMiniResponseModel>(responses);
    }

    [HttpPut("move")]
    [HttpPost("move")]
    public async Task MoveMany([FromBody] CipherBulkMoveRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only move up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.MoveManyAsync(model.Ids.Select(i => new Guid(i)),
            string.IsNullOrWhiteSpace(model.FolderId) ? (Guid?)null : new Guid(model.FolderId), userId);
    }

    [HttpPut("share")]
    [HttpPost("share")]
    public async Task PutShareMany([FromBody] CipherBulkShareRequestModel model)
    {
        var organizationId = new Guid(model.Ciphers.First().OrganizationId);
        if (!await _currentContext.OrganizationUser(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, withOrganizations: false);
        var ciphersDict = ciphers.ToDictionary(c => c.Id);

        var shareCiphers = new List<(Cipher, DateTime?)>();
        foreach (var cipher in model.Ciphers)
        {
            if (!ciphersDict.ContainsKey(cipher.Id.Value))
            {
                throw new BadRequestException("Trying to move ciphers that you do not own.");
            }

            var existingCipher = ciphersDict[cipher.Id.Value];

            ValidateClientVersionForFido2CredentialSupport(existingCipher);

            shareCiphers.Add((cipher.ToCipher(existingCipher), cipher.LastKnownRevisionDate));
        }

        await _cipherService.ShareManyAsync(shareCiphers, organizationId,
            model.CollectionIds.Select(c => new Guid(c)), userId);
    }

    [HttpPost("purge")]
    public async Task PostPurge([FromBody] SecretVerificationRequestModel model, string organizationId = null)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            ModelState.AddModelError(string.Empty, "User verification failed.");
            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        // If Account Deprovisioning is enabled, we need to check if the user is managed by any organization.
        if (_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            && await _userService.IsManagedByAnyOrganizationAsync(user.Id))
        {
            throw new BadRequestException("Cannot purge accounts owned by an organization. Contact your organization administrator for additional details.");
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            await _cipherRepository.DeleteByUserIdAsync(user.Id);
        }
        else
        {
            var orgId = new Guid(organizationId);
            if (!await _currentContext.EditAnyCollection(orgId))
            {
                throw new NotFoundException();
            }
            await _cipherService.PurgeAsync(orgId);
        }
    }

    [HttpPost("{id}/attachment/v2")]
    public async Task<AttachmentUploadDataResponseModel> PostAttachment(Guid id, [FromBody] AttachmentRequestModel request)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = request.AdminRequest ?
            await _cipherRepository.GetOrganizationDetailsByIdAsync(id) :
            await GetByIdAsync(id, userId);

        if (cipher == null || (request.AdminRequest && (!cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))))
        {
            throw new NotFoundException();
        }

        if (request.FileSize > CipherService.MAX_FILE_SIZE)
        {
            throw new BadRequestException($"Max file size is {CipherService.MAX_FILE_SIZE_READABLE}.");
        }

        var (attachmentId, uploadUrl) = await _cipherService.CreateAttachmentForDelayedUploadAsync(cipher,
            request.Key, request.FileName, request.FileSize, request.AdminRequest, userId);
        return new AttachmentUploadDataResponseModel
        {
            AttachmentId = attachmentId,
            Url = uploadUrl,
            FileUploadType = _attachmentStorageService.FileUploadType,
            CipherResponse = request.AdminRequest ? null : new CipherResponseModel((CipherDetails)cipher, _globalSettings),
            CipherMiniResponse = request.AdminRequest ? new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp) : null,
        };
    }

    [HttpGet("{id}/attachment/{attachmentId}/renew")]
    public async Task<AttachmentUploadDataResponseModel> RenewFileUploadUrl(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        var attachments = cipher?.GetAttachments();

        if (attachments == null || !attachments.ContainsKey(attachmentId) || attachments[attachmentId].Validated)
        {
            throw new NotFoundException();
        }

        return new AttachmentUploadDataResponseModel
        {
            Url = await _attachmentStorageService.GetAttachmentUploadUrlAsync(cipher, attachments[attachmentId]),
            FileUploadType = _attachmentStorageService.FileUploadType,
        };
    }

    [HttpPost("{id}/attachment/{attachmentId}")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task PostFileForExistingAttachment(Guid id, string attachmentId)
    {
        if (!Request?.ContentType.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        var attachments = cipher?.GetAttachments();
        if (attachments == null || !attachments.ContainsKey(attachmentId))
        {
            throw new NotFoundException();
        }
        var attachmentData = attachments[attachmentId];

        await Request.GetFileAsync(async (stream) =>
        {
            await _cipherService.UploadFileForExistingAttachmentAsync(stream, cipher, attachmentData);
        });
    }

    [HttpPost("{id}/attachment")]
    [Obsolete("Deprecated Attachments API", false)]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task<CipherResponseModel> PostAttachment(Guid id)
    {
        ValidateAttachment();

        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                    Request.ContentLength.GetValueOrDefault(0), userId);
        });

        return new CipherResponseModel(cipher, _globalSettings);
    }

    [HttpPost("{id}/attachment-admin")]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task<CipherMiniResponseModel> PostAttachmentAdmin(string id)
    {
        ValidateAttachment();

        var idGuid = new Guid(id);
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(idGuid);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                    Request.ContentLength.GetValueOrDefault(0), userId, true);
        });

        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpGet("{id}/attachment/{attachmentId}")]
    public async Task<AttachmentResponseModel> GetAttachmentData(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        var result = await _cipherService.GetAttachmentDownloadDataAsync(cipher, attachmentId);
        return new AttachmentResponseModel(result);
    }

    [HttpPost("{id}/attachment/{attachmentId}/share")]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task PostAttachmentShare(string id, string attachmentId, Guid organizationId)
    {
        ValidateAttachment();

        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || cipher.UserId != userId || !await _currentContext.OrganizationUser(organizationId))
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentShareAsync(cipher, stream, fileName, key,
                Request.ContentLength.GetValueOrDefault(0), attachmentId, organizationId);
        });
    }

    [HttpDelete("{id}/attachment/{attachmentId}")]
    [HttpPost("{id}/attachment/{attachmentId}/delete")]
    public async Task DeleteAttachment(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, false);
    }

    [HttpDelete("{id}/attachment/{attachmentId}/admin")]
    [HttpPost("{id}/attachment/{attachmentId}/delete-admin")]
    public async Task DeleteAttachmentAdmin(string id, string attachmentId)
    {
        var idGuid = new Guid(id);
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(idGuid);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, true);
    }

    [AllowAnonymous]
    [HttpPost("attachment/validate/azure")]
    public async Task<ObjectResult> AzureValidateFile()
    {
        return await ApiHelpers.HandleAzureEvents(Request, new Dictionary<string, Func<EventGridEvent, Task>>
        {
            {
                "Microsoft.Storage.BlobCreated", async (eventGridEvent) =>
                {
                    try
                    {
                        var blobName = eventGridEvent.Subject.Split($"{AzureAttachmentStorageService.EventGridEnabledContainerName}/blobs/")[1];
                        var (cipherId, organizationId, attachmentId) = AzureAttachmentStorageService.IdentifiersFromBlobName(blobName);
                        var cipher = await _cipherRepository.GetByIdAsync(new Guid(cipherId));
                        var attachments = cipher?.GetAttachments() ?? new Dictionary<string, CipherAttachment.MetaData>();

                        if (cipher == null || !attachments.ContainsKey(attachmentId) || attachments[attachmentId].Validated)
                        {
                            if (_attachmentStorageService is AzureSendFileStorageService azureFileStorageService)
                            {
                                await azureFileStorageService.DeleteBlobAsync(blobName);
                            }

                            return;
                        }

                        await _cipherService.ValidateCipherAttachmentFile(cipher, attachments[attachmentId]);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Uncaught exception occurred while handling event grid event: {JsonSerializer.Serialize(eventGridEvent)}");
                        return;
                    }
                }
            }
        });
    }

    private void ValidateAttachment()
    {
        if (!Request?.ContentType.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }
    }

    private void ValidateClientVersionForFido2CredentialSupport(Cipher cipher)
    {
        if (cipher.Type == Core.Vault.Enums.CipherType.Login)
        {
            var loginData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
            if (loginData?.Fido2Credentials != null && _currentContext.ClientVersion < _fido2KeyCipherMinimumVersion)
            {
                throw new BadRequestException("Cannot edit item. Update to the latest version of Bitwarden and try again.");
            }
        }
    }

    private async Task<CipherDetails> GetByIdAsync(Guid cipherId, Guid userId)
    {
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }
}
