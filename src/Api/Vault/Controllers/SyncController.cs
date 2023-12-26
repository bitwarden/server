using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Repositories;
using Bit.Core.Utilities;
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
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    private bool FlexibleCollectionsIsEnabled =>
        _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

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
        ICurrentContext currentContext,
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
        _currentContext = currentContext;
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
        // If Flexible Collections is enabled, downgrade any Manager roles to User and set Edit/Delete Assigned Collections to false
        if (FlexibleCollectionsIsEnabled)
        {
            foreach (var orgUser in organizationUserDetails)
            {
                if (orgUser.Type == OrganizationUserType.Manager)
                {
                    orgUser.Type = OrganizationUserType.User;
                }

                var permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
                permissions.EditAssignedCollections = false;
                permissions.DeleteAssignedCollections = false;
                orgUser.Permissions = CoreHelpers.ClassToJsonData(permissions);
            }
        }

        var providerUserDetails = await _providerUserRepository.GetManyDetailsByUserAsync(user.Id,
            ProviderUserStatusType.Confirmed);
        var providerUserOrganizationDetails =
            await _providerUserRepository.GetManyOrganizationDetailsByUserAsync(user.Id,
                ProviderUserStatusType.Confirmed);
        var hasEnabledOrgs = organizationUserDetails.Any(o => o.Enabled);

        var folders = await _folderRepository.GetManyByUserIdAsync(user.Id);
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(user.Id, useFlexibleCollections: FlexibleCollectionsIsEnabled, withOrganizations: hasEnabledOrgs);
        var sends = await _sendRepository.GetManyByUserIdAsync(user.Id);

        IEnumerable<CollectionDetails> collections = null;
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
        IEnumerable<Policy> policies = await _policyRepository.GetManyByUserIdAsync(user.Id);

        if (hasEnabledOrgs)
        {
            collections = await _collectionRepository.GetManyByUserIdAsync(user.Id, FlexibleCollectionsIsEnabled);
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(user.Id, FlexibleCollectionsIsEnabled);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
        }

        var userTwoFactorEnabled = await _userService.TwoFactorIsEnabledAsync(user);
        var userHasPremiumFromOrganization = await _userService.HasPremiumFromOrganization(user);
        var response = new SyncResponseModel(_globalSettings, user, userTwoFactorEnabled, userHasPremiumFromOrganization, organizationUserDetails,
            providerUserDetails, providerUserOrganizationDetails, folders, collections, ciphers,
            collectionCiphersGroupDict, excludeDomains, policies, sends);
        return response;
    }
}
