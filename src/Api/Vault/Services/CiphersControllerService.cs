using System.Security.Claims;
using Bit.Api.Vault.Models.Response;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Services;

namespace Api.Vault.Services;

public class CiphersControllerService
{
    private readonly ICipherService _cipherService;
    private readonly IUserService _userService;
    private readonly IProviderService _providerService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationCiphersQuery _organizationCiphersQuery;

    public CiphersControllerService(
        ICipherService cipherService,
        IUserService userService,
        IProviderService providerService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IFeatureService featureService,
        IApplicationCacheService applicationCacheService,
        IOrganizationCiphersQuery organizationCiphersQuery)
    {
        _cipherService = cipherService;
        _userService = userService;
        _providerService = providerService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _applicationCacheService = applicationCacheService;
        _organizationCiphersQuery = organizationCiphersQuery;
    }

    public async Task<IEnumerable<CipherMiniDetailsResponseModel>> GetOrganizationCiphers(ClaimsPrincipal user, Guid organizationId)
    {
        // Flexible Collections Logic
        if (await UseFlexibleCollectionsV1Async(organizationId))
        {
            return await GetAllOrganizationCiphersAsync(organizationId);
        }

        // Pre-Flexible Collections Logic
        var userId = _userService.GetProperUserId(user).Value;

        (IEnumerable<CipherOrganizationDetails> orgCiphers, Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict) = await _cipherService.GetOrganizationCiphers(userId, organizationId);

        var responses = orgCiphers.Select(c => new CipherMiniDetailsResponseModel(c, _globalSettings,
            collectionCiphersGroupDict, c.OrganizationUseTotp));

        var providerId = await _currentContext.ProviderIdForOrg(organizationId);
        if (providerId.HasValue)
        {
            await _providerService.LogProviderAccessToOrganizationAsync(organizationId);
        }

        return new List<CipherMiniDetailsResponseModel>(responses);
    }

    /// <summary>
    /// Returns all ciphers belonging to the organization if the user has access to All ciphers.
    /// </summary>
    /// <exception cref="NotFoundException"></exception>
    private async Task<IEnumerable<CipherMiniDetailsResponseModel>> GetAllOrganizationCiphersAsync(Guid organizationId)
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

        return new List<CipherMiniDetailsResponseModel>(allOrganizationCipherResponses);
    }

    /// <summary>
    /// TODO: Move this to its own authorization handler or equivalent service - AC-2062
    /// </summary>
    private async Task<bool> CanAccessAllCiphersAsync(Guid organizationId)
    {
        var org = _currentContext.GetOrganization(organizationId);

        // We do NOT need to check the organization collection management setting here because Owners/Admins can
        // ALWAYS access all ciphers in order to export them. Additionally, custom users with AccessImportExport or
        // EditAnyCollection permissions can also always access all ciphers.
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.AccessImportExport: true } or
        { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        // Provider users can access all ciphers in V1 (to change later)
        if (await _currentContext.ProviderUserForOrgAsync(organizationId))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> UseFlexibleCollectionsV1Async(Guid organizationId)
    {
        if (!_featureService.IsEnabled(Bit.Core.FeatureFlagKeys.FlexibleCollectionsV1))
        {
            return false;
        }

        var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return organizationAbility?.FlexibleCollections ?? false;
    }
}
