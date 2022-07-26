using Bit.Api.Models.Response;
using Bit.Core.Entities;
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
        private readonly IUserService _userService;
        private readonly ICollectionService _collectionService;
        private readonly ICipherService _cipherService;
        private readonly GlobalSettings _globalSettings;

        public OrganizationExportController(
            ICipherService cipherService,
            ICollectionService collectionService,
            IUserService userService,
            GlobalSettings globalSettings)
        {
            _cipherService = cipherService;
            _collectionService = collectionService;
            _userService = userService;
            _globalSettings = globalSettings;
        }

        [HttpGet("export")]
        public async Task<OrganizationExportResponseModel> Export(Guid organizationId)
        {
            var userId = _userService.GetProperUserId(User).Value;

            IEnumerable<Collection> orgCollections = await _collectionService.GetOrganizationCollections(organizationId);
            (IEnumerable<CipherOrganizationDetails> orgCiphers, Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict) = await _cipherService.GetOrganizationCiphers(userId, organizationId);

            var result = new OrganizationExportResponseModel
            {
                Collections = GetOrganizationCollectionsResponse(orgCollections),
                Ciphers = await GetOrganizationCiphersResponse(orgCiphers, collectionCiphersGroupDict)
            };

            return result;
        }

        private ListResponseModel<CollectionResponseModel> GetOrganizationCollectionsResponse(IEnumerable<Collection> orgCollections)
        {
            var collections = orgCollections.Select(c => new CollectionResponseModel(c));
            return new ListResponseModel<CollectionResponseModel>(collections);
        }

        private async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCiphersResponse(IEnumerable<CipherOrganizationDetails> orgCiphers,
            Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict)
        {
            var responses = orgCiphers.Select(c => new CipherMiniDetailsResponseModel(c, _globalSettings,
                collectionCiphersGroupDict, c.OrganizationUseTotp));

            return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
        }
    }
}
