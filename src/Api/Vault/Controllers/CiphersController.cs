// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Globalization;
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
using Bit.Core.Vault.Authorization.Permissions;
using Bit.Core.Vault.Commands.Interfaces;
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
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IArchiveCiphersCommand _archiveCiphersCommand;
    private readonly IUnarchiveCiphersCommand _unarchiveCiphersCommand;
    private readonly IFeatureService _featureService;

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
        IOrganizationCiphersQuery organizationCiphersQuery,
        IApplicationCacheService applicationCacheService,
        ICollectionRepository collectionRepository,
        IArchiveCiphersCommand archiveCiphersCommand,
        IUnarchiveCiphersCommand unarchiveCiphersCommand,
        IFeatureService featureService)
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
        _organizationCiphersQuery = organizationCiphersQuery;
        _applicationCacheService = applicationCacheService;
        _collectionRepository = collectionRepository;
        _archiveCiphersCommand = archiveCiphersCommand;
        _unarchiveCiphersCommand = unarchiveCiphersCommand;
        _featureService = featureService;
    }

    [HttpGet("{id}")]
    public async Task<CipherResponseModel> Get(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();

        return new CipherResponseModel(cipher, user, organizationAbilities, _globalSettings);
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

    [HttpGet("{id}/details")]
    public async Task<CipherDetailsResponseModel> GetDetails(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id);
        return new CipherDetailsResponseModel(cipher, user, organizationAbilities, _globalSettings, collectionCiphers);
    }

    [HttpGet("{id}/full-details")]
    [Obsolete("This endpoint is deprecated. Use GET details method instead.")]
    public async Task<CipherDetailsResponseModel> GetFullDetails(Guid id)
    {
        return await GetDetails(id);
    }

    [HttpGet("")]
    public async Task<ListResponseModel<CipherDetailsResponseModel>> GetAll()
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var hasOrgs = _currentContext.Organizations.Count != 0;
        // TODO: Use hasOrgs proper for cipher listing here?
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(user.Id, withOrganizations: true);
        Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
        if (hasOrgs)
        {
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(user.Id);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
        }
        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var responses = ciphers.Select(cipher => new CipherDetailsResponseModel(
            cipher,
            user,
            organizationAbilities,
            _globalSettings,
            collectionCiphersGroupDict)).ToList();
        return new ListResponseModel<CipherDetailsResponseModel>(responses);
    }

    [HttpPost("")]
    public async Task<CipherResponseModel> Post([FromBody] CipherRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        // Validate the model was encrypted for the posting user
        if (model.EncryptedFor != null)
        {
            if (model.EncryptedFor != user.Id)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", user.Id, model.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

        var cipher = model.ToCipherDetails(user.Id);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, user.Id, model.LastKnownRevisionDate, null, cipher.OrganizationId.HasValue);
        var response = new CipherResponseModel(
            cipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
        return response;
    }

    [HttpPost("create")]
    public async Task<CipherResponseModel> PostCreate([FromBody] CipherCreateRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);

        // Validate the model was encrypted for the posting user
        if (model.Cipher.EncryptedFor != null)
        {
            if (model.Cipher.EncryptedFor != user.Id)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", user.Id, model.Cipher.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

        var cipher = model.Cipher.ToCipherDetails(user.Id);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, user.Id, model.Cipher.LastKnownRevisionDate, model.CollectionIds, cipher.OrganizationId.HasValue);
        return await Get(cipher.Id);
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

        // Validate the model was encrypted for the posting user
        if (model.Cipher.EncryptedFor != null)
        {
            if (model.Cipher.EncryptedFor != userId)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", userId, model.Cipher.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

        await _cipherService.SaveAsync(cipher, userId, model.Cipher.LastKnownRevisionDate, model.CollectionIds, true, false);

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);
        return response;
    }

    [HttpPut("{id}")]
    public async Task<CipherResponseModel> Put(Guid id, [FromBody] CipherRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        // Validate the model was encrypted for the posting user
        if (model.EncryptedFor != null)
        {
            if (model.EncryptedFor != user.Id)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CipherId: {CipherId}, CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", id, user.Id, model.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

        ValidateClientVersionForFido2CredentialSupport(cipher);

        var collectionIds = (await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id)).Select(c => c.CollectionId).ToList();
        var modelOrgId = string.IsNullOrWhiteSpace(model.OrganizationId) ?
            (Guid?)null : new Guid(model.OrganizationId);
        if (cipher.OrganizationId != modelOrgId)
        {
            throw new BadRequestException("Organization mismatch. Re-sync if you recently moved this item, " +
                "then try again.");
        }

        await _cipherService.SaveDetailsAsync(model.ToCipherDetails(cipher), user.Id, model.LastKnownRevisionDate, collectionIds);

        var response = new CipherResponseModel(
            cipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
        return response;
    }

    [HttpPost("{id}")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherResponseModel> PostPut(Guid id, [FromBody] CipherRequestModel model)
    {
        return await Put(id, model);
    }

    [HttpPut("{id}/admin")]
    public async Task<CipherMiniResponseModel> PutAdmin(Guid id, [FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(id);

        // Validate the model was encrypted for the posting user
        if (model.EncryptedFor != null)
        {
            if (model.EncryptedFor != userId)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CipherId: {CipherId}, CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", id, userId, model.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

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

    [HttpPost("{id}/admin")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherMiniResponseModel> PostPutAdmin(Guid id, [FromBody] CipherRequestModel model)
    {
        return await PutAdmin(id, model);
    }

    [HttpGet("organization-details")]
    public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCiphers(Guid organizationId, bool includeMemberItems = false)
    {
        if (!await CanAccessAllCiphersAsync(organizationId))
        {
            throw new NotFoundException();
        }

        bool excludeDefaultUserCollections = _featureService.IsEnabled(FeatureFlagKeys.CreateDefaultLocation) && !includeMemberItems;
        var allOrganizationCiphers = excludeDefaultUserCollections
        ?
            await _organizationCiphersQuery.GetAllOrganizationCiphersExcludingDefaultUserCollections(organizationId)
        :
            await _organizationCiphersQuery.GetAllOrganizationCiphers(organizationId);

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

        var user = await _userService.GetUserByPrincipalAsync(User);
        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var responses = ciphers.Select(cipher =>
            new CipherDetailsResponseModel(
                cipher,
                user,
                organizationAbilities,
                _globalSettings));

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

        // If we're not an "admin" we don't need to check the ciphers
        if (org is not ({ Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
            { Permissions.EditAnyCollection: true }))
        {
            return false;
        }

        // We know we're an "admin", now check the ciphers explicitly (in case admins are restricted)
        return await CanEditCiphersAsync(organizationId, cipherIds);
    }

    private async Task<bool> CanDeleteOrRestoreCipherAsAdminAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // If we're not an "admin" we don't need to check the ciphers
        if (org is not ({ Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
            { Permissions.EditAnyCollection: true }))
        {
            return false;
        }

        // If the user can edit all ciphers for the organization, just check they all belong to the org
        if (await CanEditAllCiphersAsync(organizationId))
        {
            // TODO: This can likely be optimized to only query the requested ciphers and then checking they belong to the org
            var orgCiphers = (await _cipherRepository.GetManyByOrganizationIdAsync(organizationId)).ToDictionary(c => c.Id);

            // Ensure all requested ciphers are in orgCiphers
            return cipherIds.All(c => orgCiphers.ContainsKey(c));
        }

        // The user cannot access any ciphers for the organization, we're done
        if (!await CanAccessOrganizationCiphersAsync(organizationId))
        {
            return false;
        }

        var user = await _userService.GetUserByPrincipalAsync(User);
        // Select all deletable ciphers for this user belonging to the organization
        var deletableOrgCipherList = (await _cipherRepository.GetManyByUserIdAsync(user.Id, true))
            .Where(c => c.OrganizationId == organizationId && c.UserId == null).ToList();

        // Special case for unassigned ciphers
        if (await CanAccessUnassignedCiphersAsync(organizationId))
        {
            var unassignedCiphers =
                (await _cipherRepository.GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(
                    organizationId));

            // Users that can access unassigned ciphers can also delete them
            deletableOrgCipherList.AddRange(unassignedCiphers.Select(c => new CipherDetails(c) { Manage = true }));
        }

        var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        var deletableOrgCiphers = deletableOrgCipherList
            .Where(c => NormalCipherPermissions.CanDelete(user, c, organizationAbility))
            .ToDictionary(c => c.Id);

        return cipherIds.All(c => deletableOrgCiphers.ContainsKey(c));
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

        // Provider users cannot edit ciphers
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return false;
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

        // Provider users cannot access organization ciphers
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return false;
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

        // Provider users cannot access ciphers
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanModifyCipherCollectionsAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
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
            .Where(c => c.OrganizationId == organizationId && c.UserId == null && c.Edit && c.ViewPassword).ToList();

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
    public async Task<CipherResponseModel> PutPartial(Guid id, [FromBody] CipherPartialRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var folderId = string.IsNullOrWhiteSpace(model.FolderId) ? null : (Guid?)new Guid(model.FolderId);
        await _cipherRepository.UpdatePartialAsync(id, user.Id, folderId, model.Favorite);

        var cipher = await GetByIdAsync(id, user.Id);
        var response = new CipherResponseModel(
            cipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
        return response;
    }

    [HttpPost("{id}/partial")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherResponseModel> PostPartial(Guid id, [FromBody] CipherPartialRequestModel model)
    {
        return await PutPartial(id, model);
    }

    [HttpPut("{id}/share")]
    public async Task<CipherResponseModel> PutShare(Guid id, [FromBody] CipherShareRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await _cipherRepository.GetByIdAsync(id);
        if (cipher == null || cipher.UserId != user.Id ||
            !await _currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
        {
            throw new NotFoundException();
        }

        // Validate the model was encrypted for the posting user
        if (model.Cipher.EncryptedFor != null)
        {
            if (model.Cipher.EncryptedFor != user.Id)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CipherId: {CipherId} CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", id, user.Id, model.Cipher.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }
        }

        if (cipher.ArchivedDate.HasValue)
        {
            throw new BadRequestException("Cannot move an archived item to an organization.");
        }

        ValidateClientVersionForFido2CredentialSupport(cipher);

        var original = cipher.Clone();
        await _cipherService.ShareAsync(original, model.Cipher.ToCipher(cipher), new Guid(model.Cipher.OrganizationId),
            model.CollectionIds.Select(c => new Guid(c)), user.Id, model.Cipher.LastKnownRevisionDate);

        var sharedCipher = await GetByIdAsync(id, user.Id);
        var response = new CipherResponseModel(
            sharedCipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
        return response;
    }

    [HttpPost("{id}/share")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherResponseModel> PostShare(Guid id, [FromBody] CipherShareRequestModel model)
    {
        return await PutShare(id, model);
    }

    [HttpPut("{id}/collections")]
    public async Task<CipherDetailsResponseModel> PutCollections(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher,
            model.CollectionIds.Select(c => new Guid(c)), user.Id, false);

        var updatedCipher = await GetByIdAsync(id, user.Id);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id);

        return new CipherDetailsResponseModel(
            updatedCipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings,
            collectionCiphers);
    }

    [HttpPost("{id}/collections")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherDetailsResponseModel> PostCollections(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        return await PutCollections(id, model);
    }

    [HttpPut("{id}/collections_v2")]
    public async Task<OptionalCipherDetailsResponseModel> PutCollections_vNext(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.OrganizationUser(cipher.OrganizationId.Value) || !cipher.ViewPassword)
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher,
            model.CollectionIds.Select(c => new Guid(c)), user.Id, false);

        var updatedCipher = await GetByIdAsync(id, user.Id);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(user.Id, id);
        // If a user removes the last Can Manage access of a cipher, the "updatedCipher" will return null
        // We will be returning an "Unavailable" property so the client knows the user can no longer access this
        var response = new OptionalCipherDetailsResponseModel()
        {
            Unavailable = updatedCipher is null,
            Cipher = updatedCipher is null
                ? null
                : new CipherDetailsResponseModel(
                    updatedCipher,
                    user,
                    await _applicationCacheService.GetOrganizationAbilitiesAsync(),
                    _globalSettings,
                    collectionCiphers)
        };
        return response;
    }

    [HttpPost("{id}/collections_v2")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<OptionalCipherDetailsResponseModel> PostCollections_vNext(Guid id, [FromBody] CipherCollectionsRequestModel model)
    {
        return await PutCollections_vNext(id, model);
    }

    [HttpPut("{id}/collections-admin")]
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

    [HttpPost("{id}/collections-admin")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<CipherMiniDetailsResponseModel> PostCollectionsAdmin(string id, [FromBody] CipherCollectionsRequestModel model)
    {
        return await PutCollectionsAdmin(id, model);
    }

    [HttpPost("bulk-collections")]
    public async Task PostBulkCollections([FromBody] CipherBulkUpdateCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.ValidateBulkCollectionAssignmentAsync(model.CollectionIds, model.CipherIds, userId);

        if (!await CanModifyCipherCollectionsAsync(model.OrganizationId, model.CipherIds) ||
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

    [HttpPut("{id}/archive")]
    [RequireFeature(FeatureFlagKeys.ArchiveVaultItems)]
    public async Task<CipherMiniResponseModel> PutArchive(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var archivedCipherOrganizationDetails = await _archiveCiphersCommand.ArchiveManyAsync([id], userId);

        if (archivedCipherOrganizationDetails.Count == 0)
        {
            throw new BadRequestException("Cipher was not archived. Ensure the provided ID is correct and you have permission to archive it.");
        }

        return new CipherMiniResponseModel(archivedCipherOrganizationDetails.First(), _globalSettings, archivedCipherOrganizationDetails.First().OrganizationUseTotp);
    }

    [HttpPut("archive")]
    [RequireFeature(FeatureFlagKeys.ArchiveVaultItems)]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PutArchiveMany([FromBody] CipherBulkArchiveRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only archive up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;

        var cipherIdsToArchive = new HashSet<Guid>(model.Ids);

        var archivedCiphers = await _archiveCiphersCommand.ArchiveManyAsync(cipherIdsToArchive, userId);

        if (archivedCiphers.Count == 0)
        {
            throw new BadRequestException("No ciphers were archived. Ensure the provided IDs are correct and you have permission to archive them.");
        }

        var responses = archivedCiphers.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));

        return new ListResponseModel<CipherMiniResponseModel>(responses);
    }

    [HttpDelete("{id}")]
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

    [HttpPost("{id}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task PostDelete(Guid id)
    {
        await Delete(id);
    }

    [HttpDelete("{id}/admin")]
    public async Task DeleteAdmin(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanDeleteOrRestoreCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAsync(cipher, userId, true);
    }

    [HttpPost("{id}/delete-admin")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task PostDeleteAdmin(Guid id)
    {
        await DeleteAdmin(id);
    }

    [HttpDelete("")]
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

    [HttpPost("delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task PostDeleteMany([FromBody] CipherBulkDeleteRequestModel model)
    {
        await DeleteMany(model);
    }

    [HttpDelete("admin")]
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
            !await CanDeleteOrRestoreCipherAsAdminAsync(new Guid(model.OrganizationId), cipherIds))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.DeleteManyAsync(cipherIds, userId, new Guid(model.OrganizationId), true);
    }

    [HttpPost("delete-admin")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task PostDeleteManyAdmin([FromBody] CipherBulkDeleteRequestModel model)
    {
        await DeleteManyAdmin(model);
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
    public async Task PutDeleteAdmin(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsyncAdmin(id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanDeleteOrRestoreCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.SoftDeleteAsync(new CipherDetails(cipher), userId, true);
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
            !await CanDeleteOrRestoreCipherAsAdminAsync(new Guid(model.OrganizationId), cipherIds))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SoftDeleteManyAsync(cipherIds, userId, new Guid(model.OrganizationId), true);
    }

    [HttpPut("{id}/unarchive")]
    [RequireFeature(FeatureFlagKeys.ArchiveVaultItems)]
    public async Task<CipherMiniResponseModel> PutUnarchive(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var unarchivedCipherDetails = await _unarchiveCiphersCommand.UnarchiveManyAsync([id], userId);

        if (unarchivedCipherDetails.Count == 0)
        {
            throw new BadRequestException("Cipher was not unarchived. Ensure the provided ID is correct and you have permission to archive it.");
        }

        return new CipherMiniResponseModel(unarchivedCipherDetails.First(), _globalSettings, unarchivedCipherDetails.First().OrganizationUseTotp);
    }

    [HttpPut("unarchive")]
    [RequireFeature(FeatureFlagKeys.ArchiveVaultItems)]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PutUnarchiveMany([FromBody] CipherBulkUnarchiveRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only unarchive up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;

        var cipherIdsToUnarchive = new HashSet<Guid>(model.Ids);

        var unarchivedCipherOrganizationDetails = await _unarchiveCiphersCommand.UnarchiveManyAsync(cipherIdsToUnarchive, userId);

        if (unarchivedCipherOrganizationDetails.Count == 0)
        {
            throw new BadRequestException("Ciphers were not unarchived. Ensure the provided ID is correct and you have permission to archive it.");
        }

        var responses = unarchivedCipherOrganizationDetails.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));

        return new ListResponseModel<CipherMiniResponseModel>(responses);
    }

    [HttpPut("{id}/restore")]
    public async Task<CipherResponseModel> PutRestore(Guid id)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(cipher, user.Id);
        return new CipherResponseModel(
            cipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
    }

    [HttpPut("{id}/restore-admin")]
    public async Task<CipherMiniResponseModel> PutRestoreAdmin(Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsyncAdmin(id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanDeleteOrRestoreCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(new CipherDetails(cipher), userId, true);
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

        if (model.OrganizationId == default || !await CanDeleteOrRestoreCipherAsAdminAsync(model.OrganizationId, cipherIdsToRestore))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;

        var restoredCiphers = await _cipherService.RestoreManyAsync(cipherIdsToRestore, userId, model.OrganizationId, true);
        var responses = restoredCiphers.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));
        return new ListResponseModel<CipherMiniResponseModel>(responses);
    }

    [HttpPut("move")]
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

    [HttpPost("move")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task PostMoveMany([FromBody] CipherBulkMoveRequestModel model)
    {
        await MoveMany(model);
    }

    [HttpPut("share")]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PutShareMany([FromBody] CipherBulkShareRequestModel model)
    {
        var organizationId = new Guid(model.Ciphers.First().OrganizationId);
        if (!await _currentContext.OrganizationUser(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, withOrganizations: false);
        var ciphersDict = ciphers.ToDictionary(c => c.Id);

        // Validate the model was encrypted for the posting user
        foreach (var cipher in model.Ciphers)
        {
            if (cipher.EncryptedFor.HasValue && cipher.EncryptedFor.Value != userId)
            {
                _logger.LogError("Cipher was not encrypted for the current user. CipherId: {CipherId}, CurrentUser: {CurrentUserId}, EncryptedFor: {EncryptedFor}", cipher.Id, userId, cipher.EncryptedFor);
                throw new BadRequestException("Cipher was not encrypted for the current user. Please try again.");
            }

            if (cipher.ArchivedDate.HasValue)
            {
                throw new BadRequestException("Cannot move archived items to an organization.");
            }
        }

        var shareCiphers = new List<(CipherDetails, DateTime?)>();
        foreach (var cipher in model.Ciphers)
        {
            if (!ciphersDict.TryGetValue(cipher.Id.Value, out var existingCipher))
            {
                throw new BadRequestException("Trying to share ciphers that you do not own.");
            }

            ValidateClientVersionForFido2CredentialSupport(existingCipher);

            if (existingCipher.ArchivedDate.HasValue)
            {
                throw new BadRequestException("Cannot move archived items to an organization.");
            }

            shareCiphers.Add((cipher.ToCipherDetails(existingCipher), cipher.LastKnownRevisionDate));
        }

        var updated = await _cipherService.ShareManyAsync(
            shareCiphers,
            organizationId,
            model.CollectionIds.Select(Guid.Parse),
            userId
        );

        var response = updated.Select(c => new CipherMiniResponseModel(c, _globalSettings, c.OrganizationUseTotp));
        return new ListResponseModel<CipherMiniResponseModel>(response);
    }

    [HttpPost("share")]
    [Obsolete("This endpoint is deprecated. Use PUT method instead.")]
    public async Task<ListResponseModel<CipherMiniResponseModel>> PostShareMany([FromBody] CipherBulkShareRequestModel model)
    {
        return await PutShareMany(model);
    }

    [HttpPost("purge")]
    public async Task PostPurge([FromBody] SecretVerificationRequestModel model, Guid? organizationId = null)
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

        if (organizationId == null)
        {
            // Check if the user is claimed by any organization.
            if (await _userService.IsClaimedByAnyOrganizationAsync(user.Id))
            {
                throw new BadRequestException("Cannot purge accounts owned by an organization. Contact your organization administrator for additional details.");
            }
            await _cipherRepository.DeleteByUserIdAsync(user.Id);
        }
        else
        {
            if (!await _currentContext.EditAnyCollection(organizationId!.Value))
            {
                throw new NotFoundException();
            }
            await _cipherService.PurgeAsync(organizationId!.Value);
        }
    }

    [HttpPost("{id}/attachment/v2")]
    public async Task<AttachmentUploadDataResponseModel> PostAttachment(Guid id, [FromBody] AttachmentRequestModel request)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = request.AdminRequest ?
            await _cipherRepository.GetOrganizationDetailsByIdAsync(id) :
            await GetByIdAsync(id, user.Id);

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
            request.Key, request.FileName, request.FileSize, request.AdminRequest, user.Id, request.LastKnownRevisionDate);
        return new AttachmentUploadDataResponseModel
        {
            AttachmentId = attachmentId,
            Url = uploadUrl,
            FileUploadType = _attachmentStorageService.FileUploadType,
            CipherResponse = request.AdminRequest ? null : new CipherResponseModel(
                (CipherDetails)cipher,
                user,
                await _applicationCacheService.GetOrganizationAbilitiesAsync(),
                _globalSettings),
            CipherMiniResponse = request.AdminRequest ? new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp) : null,
        };
    }

    [HttpGet("{id}/attachment/{attachmentId}/renew")]
    public async Task<AttachmentUploadDataResponseModel> RenewFileUploadUrl(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        var attachments = cipher?.GetAttachments();

        if (attachments == null || !attachments.TryGetValue(attachmentId, out var attachment) || attachment.Validated)
        {
            throw new NotFoundException();
        }

        return new AttachmentUploadDataResponseModel
        {
            Url = await _attachmentStorageService.GetAttachmentUploadUrlAsync(cipher, attachment),
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
        if (attachments == null || !attachments.TryGetValue(attachmentId, out var attachmentData))
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream) =>
        {
            await _cipherService.UploadFileForExistingAttachmentAsync(stream, cipher, attachmentData);
        });
    }

    [HttpPost("{id}/attachment")]
    [Obsolete("Deprecated Attachments API", false)]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task<CipherResponseModel> PostAttachmentV1(Guid id)
    {
        ValidateAttachment();

        var user = await _userService.GetUserByPrincipalAsync(User);
        var cipher = await GetByIdAsync(id, user.Id);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        // Extract lastKnownRevisionDate from form data if present
        DateTime? lastKnownRevisionDate = GetLastKnownRevisionDateFromForm();
        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                    Request.ContentLength.GetValueOrDefault(0), user.Id, false, lastKnownRevisionDate);
        });

        return new CipherResponseModel(
            cipher,
            user,
            await _applicationCacheService.GetOrganizationAbilitiesAsync(),
            _globalSettings);
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

        // Extract lastKnownRevisionDate from form data if present
        DateTime? lastKnownRevisionDate = GetLastKnownRevisionDateFromForm();

        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                    Request.ContentLength.GetValueOrDefault(0), userId, true, lastKnownRevisionDate);
        });

        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpGet("{id}/attachment/{attachmentId}/admin")]
    public async Task<AttachmentResponseModel> GetAttachmentDataAdmin(Guid id, string attachmentId)
    {
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        var result = await _cipherService.GetAttachmentDownloadDataAsync(cipher, attachmentId);
        return new AttachmentResponseModel(result);
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
    public async Task<DeleteAttachmentResponseData> DeleteAttachment(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        return await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, false);
    }

    [HttpPost("{id}/attachment/{attachmentId}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task<DeleteAttachmentResponseData> PostDeleteAttachment(Guid id, string attachmentId)
    {
        return await DeleteAttachment(id, attachmentId);
    }

    [HttpDelete("{id}/attachment/{attachmentId}/admin")]
    public async Task<DeleteAttachmentResponseData> DeleteAttachmentAdmin(Guid id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await CanEditCipherAsAdminAsync(cipher.OrganizationId.Value, new[] { cipher.Id }))
        {
            throw new NotFoundException();
        }

        return await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, true);
    }

    [HttpPost("{id}/attachment/{attachmentId}/delete-admin")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead.")]
    public async Task<DeleteAttachmentResponseData> PostDeleteAttachmentAdmin(Guid id, string attachmentId)
    {
        return await DeleteAttachmentAdmin(id, attachmentId);
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

                        if (cipher == null || !attachments.TryGetValue(attachmentId, out var attachment) || attachment.Validated)
                        {
                            if (_attachmentStorageService is AzureSendFileStorageService azureFileStorageService)
                            {
                                await azureFileStorageService.DeleteBlobAsync(blobName);
                            }

                            return;
                        }

                        await _cipherService.ValidateCipherAttachmentFile(cipher, attachment);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Uncaught exception occurred while handling event grid event: {Event}", JsonSerializer.Serialize(eventGridEvent));
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

    private async Task<CipherOrganizationDetails> GetByIdAsyncAdmin(Guid cipherId)
    {
        return await _cipherRepository.GetOrganizationDetailsByIdAsync(cipherId);
    }

    private async Task<CipherDetails> GetByIdAsync(Guid cipherId, Guid userId)
    {
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }

    private DateTime? GetLastKnownRevisionDateFromForm()
    {
        DateTime? lastKnownRevisionDate = null;
        if (Request.Form.TryGetValue("lastKnownRevisionDate", out var dateValue))
        {
            if (!DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
            {
                throw new BadRequestException("Invalid lastKnownRevisionDate format.");
            }
            lastKnownRevisionDate = parsedDate;
        }

        return lastKnownRevisionDate;
    }
}
