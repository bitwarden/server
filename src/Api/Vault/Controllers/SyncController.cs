using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("sync")]
[Authorize("Application")]
public class SyncController : Controller
{
    private readonly IUserService _userService;
    private readonly IFolderRepository _folderRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISendRepository _sendRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;

    public SyncController(
        IUserService userService,
        IFolderRepository folderRepository,
        ICipherRepository cipherRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IPolicyRepository policyRepository,
        ISendRepository sendRepository,
        GlobalSettings globalSettings,
        IFeatureService featureService)
    {
        _userService = userService;
        _folderRepository = folderRepository;
        _cipherRepository = cipherRepository;
        _collectionRepository = collectionRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _policyRepository = policyRepository;
        _sendRepository = sendRepository;
        _globalSettings = globalSettings;
        _featureService = featureService;
    }

    [HttpGet("")]
    public async Task<SyncResponseModel> Get([FromQuery] bool excludeDomains = false)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new BadRequestException("User not found.");
        }

        var organizationUserDetails = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id,
            OrganizationUserStatusType.Confirmed);
        var providerUserDetails = await _providerUserRepository.GetManyDetailsByUserAsync(user.Id,
            ProviderUserStatusType.Confirmed);
        var providerUserOrganizationDetails =
            await _providerUserRepository.GetManyOrganizationDetailsByUserAsync(user.Id,
                ProviderUserStatusType.Confirmed);
        var hasEnabledOrgs = organizationUserDetails.Any(o => o.Enabled);

        var folders = await _folderRepository.GetManyByUserIdAsync(user.Id);
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(user.Id, withOrganizations: hasEnabledOrgs);
        var sends = await _sendRepository.GetManyByUserIdAsync(user.Id);

        IEnumerable<CollectionDetails> collections = null;
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
        IEnumerable<Policy> policies = await _policyRepository.GetManyByUserIdAsync(user.Id);

        if (hasEnabledOrgs)
        {
            collections = await _collectionRepository.GetManyByUserIdAsync(user.Id);
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(user.Id);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
        }

        var userTwoFactorEnabled = await _userService.TwoFactorIsEnabledAsync(user);
        var userHasPremiumFromOrganization = await _userService.HasPremiumFromOrganization(user);

#nullable enable
        var organizationIdsManagingActiveUser = await GetOrganizationIdsManagingUserAsync(user, organizationUserDetails);
#nullable disable

        var response = new SyncResponseModel(_globalSettings, user, userTwoFactorEnabled, userHasPremiumFromOrganization,
            organizationIdsManagingActiveUser, organizationUserDetails, providerUserDetails, providerUserOrganizationDetails,
            folders, collections, ciphers, collectionCiphersGroupDict, excludeDomains, policies, sends);
        return response;
    }

#nullable enable
    /// <summary>
    /// Gets the IDs of the organizations that manage a user.
    /// </summary>
    /// <remarks>
    /// Organizations are considered to manage a user if the user's email domain is verified by the organization and the user is a member of it.
    /// The organization must be enabled and able to have verified domains.
    /// </remarks>
    private async Task<IEnumerable<Guid>?> GetOrganizationIdsManagingUserAsync(User user, IEnumerable<OrganizationUserOrganizationDetails> organizationUserDetails)
    {
        // Account deprovisioning must be enabled and organizations must be enabled and able to have verified domains.
        // TODO: Replace "UseSso" with a new organization ability like "UseOrganizationDomains" (PM-11622).
        if (!_featureService.IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            || !organizationUserDetails.Any(o => o is { Enabled: true, UseSso: true }))
        {
            return null;
        }

        var organizationsManagingUser = await _userService.GetOrganizationsManagingUserAsync(user.Id);
        return organizationsManagingUser?.Select(o => o.Id);
    }
#nullable disable
}
