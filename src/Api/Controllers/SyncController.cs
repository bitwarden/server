using Bit.Api.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

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
        GlobalSettings globalSettings)
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
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(user.Id, hasEnabledOrgs);
        var sends = await _sendRepository.GetManyByUserIdAsync(user.Id);

        IEnumerable<CollectionDetails> collections = null;
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
        IEnumerable<Policy> policies = null;
        if (hasEnabledOrgs)
        {
            collections = await _collectionRepository.GetManyByUserIdAsync(user.Id);
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(user.Id);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
            policies = await _policyRepository.GetManyByUserIdAsync(user.Id);
        }

        var userTwoFactorEnabled = await _userService.TwoFactorIsEnabledAsync(user);
        var userHasPremiumFromOrganization = await _userService.HasPremiumFromOrganization(user);
        var response = new SyncResponseModel(_globalSettings, user, userTwoFactorEnabled, userHasPremiumFromOrganization, organizationUserDetails,
            providerUserDetails, providerUserOrganizationDetails, folders, collections, ciphers,
            collectionCiphersGroupDict, excludeDomains, policies, sends);
        return response;
    }
}
