using Bit.Api.Models.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Core.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers
{
    [Route("organizations/{organizationId}")]
    [Authorize("Application")]
    public class OrganizationExportController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICollectionCipherRepository _collectionCipherRepository;
        private readonly ICollectionRepository _collectionRepository;
        private readonly ICurrentContext _currentContext;
        private readonly IProviderService _providerService;
        private readonly IUserService _userService;
        private readonly GlobalSettings _globalSettings;

        public OrganizationExportController(
            ICipherRepository cipherRepository,
            ICollectionCipherRepository collectionCipherRepository,
            ICollectionRepository collectionRepository,
            ICurrentContext currentContext,
            IProviderService providerService,
            IUserService userService,
            GlobalSettings globalSettings)
        {
            _cipherRepository = cipherRepository;
            _collectionCipherRepository = collectionCipherRepository;
            _collectionRepository = collectionRepository;
            _currentContext = currentContext;
            _providerService = providerService;
            _userService = userService;
            _globalSettings = globalSettings;
        }

        [HttpGet("export")]
        public async Task<OrganizationExportResponseModel> Export(Guid organizationId)
        {
            if (!await _currentContext.ViewAllCollections(organizationId) && !await _currentContext.ManageUsers(organizationId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;

            IEnumerable<Collection> orgCollections;
            IEnumerable<CipherOrganizationDetails> orgCiphers;

            if (await _currentContext.OrganizationAdmin(organizationId))
            {
                // Admins, Owners and Providers can access all items even if not assigned to them
                orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(organizationId);
                orgCiphers = await _cipherRepository.GetManyOrganizationDetailsByOrganizationIdAsync(organizationId);
            }
            else
            {
                var collections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value);
                orgCollections = collections.Where(c => c.OrganizationId == organizationId);

                var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, true);
                orgCiphers = ciphers.Where(c => c.OrganizationId == organizationId);
            }

            var result = new OrganizationExportResponseModel
            {
                Collections = GetOrganizationCollectionsResponse(orgCollections),
                Ciphers = await GetOrganizationCiphersResponse(organizationId, orgCiphers)
            };

            return result;
        }

        private ListResponseModel<CollectionResponseModel> GetOrganizationCollectionsResponse(IEnumerable<Collection> orgCollections)
        {
            var collections = orgCollections.Select(c => new CollectionResponseModel(c));
            return new ListResponseModel<CollectionResponseModel>(collections);
        }

        private async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCiphersResponse(Guid organizationId, IEnumerable<CipherOrganizationDetails> orgCiphers)
        {
            var orgCipherIds = orgCiphers.Select(c => c.Id);

            var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);
            var collectionCiphersGroupDict = collectionCiphers
                .Where(c => orgCipherIds.Contains(c.CipherId))
                .GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

            var responses = orgCiphers.Select(c => new CipherMiniDetailsResponseModel(c, _globalSettings,
                collectionCiphersGroupDict, c.OrganizationUseTotp));

            var providerId = await _currentContext.ProviderIdForOrg(organizationId);
            if (providerId.HasValue)
            {
                await _providerService.LogProviderAccessToOrganizationAsync(organizationId);
            }

            return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
        }
    }
}
