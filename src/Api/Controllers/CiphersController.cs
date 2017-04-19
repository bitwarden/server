using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Models.Api;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core;

namespace Bit.Api.Controllers
{
    [Route("ciphers")]
    [Authorize("Application")]
    public class CiphersController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly IFolderRepository _folderRepository;
        private readonly ISubvaultCipherRepository _subvaultCipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;
        private readonly CurrentContext _currentContext;

        public CiphersController(
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            ISubvaultCipherRepository subvaultCipherRepository,
            ICipherService cipherService,
            IUserService userService,
            CurrentContext currentContext)
        {
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _subvaultCipherRepository = subvaultCipherRepository;
            _cipherService = cipherService;
            _userService = userService;
            _currentContext = currentContext;
        }

        [HttpGet("{id}")]
        public async Task<CipherResponseModel> Get(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            return new CipherResponseModel(cipher);
        }

        [HttpGet("{id}/full-details")]
        public async Task<CipherFullDetailsResponseModel> GetDetails(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipherId = new Guid(id);
            var cipher = await _cipherRepository.GetFullDetailsByIdAsync(cipherId, userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            var subvaultCiphers = await _subvaultCipherRepository.GetManyByUserIdCipherIdAsync(userId, cipherId);
            return new CipherFullDetailsResponseModel(cipher, subvaultCiphers);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CipherResponseModel>> Get(bool includeFolders = true, bool includeShared = false)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId);

            // For backwards compat, do not include shared ciphers. Can be removed in a future release.
            if(!includeShared)
            {
                ciphers = ciphers.Where(c => !c.OrganizationId.HasValue).ToList();
            }

            var responses = ciphers.Select(c => new CipherResponseModel(c)).ToList();

            // Folders are included for backwards compat. Can be removed in a future release.
            if(includeFolders)
            {
                var folders = await _folderRepository.GetManyByUserIdAsync(userId);
                responses.AddRange(folders.Select(f => new CipherResponseModel(f)));
            }

            return new ListResponseModel<CipherResponseModel>(responses);
        }

        [HttpGet("details")]
        public async Task<ListResponseModel<CipherDetailsResponseModel>> GetSubvaults()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdHasSubvaultsAsync(userId);

            var subvaultCiphers = await _subvaultCipherRepository.GetManyByUserIdAsync(userId);
            var subvaultCiphersGroupDict = subvaultCiphers.GroupBy(s => s.CipherId).ToDictionary(s => s.Key);

            var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, subvaultCiphersGroupDict));
            return new ListResponseModel<CipherDetailsResponseModel>(responses);
        }

        [HttpGet("organization-details")]
        public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationSubvaults(string organizationId)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var orgIdGuid = new Guid(organizationId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(orgIdGuid);

            var subvaultCiphers = await _subvaultCipherRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var subvaultCiphersGroupDict = subvaultCiphers.GroupBy(s => s.CipherId).ToDictionary(s => s.Key);

            var responses = ciphers.Select(c => new CipherMiniDetailsResponseModel(c, subvaultCiphersGroupDict));
            return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
        }

        [Obsolete]
        [HttpGet("history")]
        public Task<CipherHistoryResponseModel> Get(DateTime since)
        {
            return Task.FromResult(new CipherHistoryResponseModel());
        }

        [HttpPost("import")]
        public async Task PostImport([FromBody]ImportRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folderCiphers = model.Folders.Select(f => f.ToFolder(userId)).ToList();
            var otherCiphers = model.Logins.Select(s => s.ToCipherDetails(userId)).ToList();

            await _cipherService.ImportCiphersAsync(
                folderCiphers,
                otherCiphers,
                model.FolderRelationships);
        }

        [Obsolete]
        [HttpPut("{id}/favorite")]
        [HttpPost("{id}/favorite")]
        public async Task Favorite(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            await _cipherRepository.UpdatePartialAsync(new Guid(id), userId, cipher.FolderId, !cipher.Favorite);
        }

        [HttpPut("{id}/partial")]
        [HttpPost("{id}/partial")]
        public async Task PutPartial(string id, [FromBody]CipherPartialRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folderId = string.IsNullOrWhiteSpace(model.FolderId) ? null : (Guid?)new Guid(model.FolderId);
            await _cipherRepository.UpdatePartialAsync(new Guid(id), userId, folderId, model.Favorite);
        }

        [HttpPut("{id}/share")]
        [HttpPost("{id}/share")]
        public async Task PutShare(string id, [FromBody]CipherShareRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null || cipher.UserId != userId ||
                !_currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
            {
                throw new NotFoundException();
            }

            await _cipherService.ShareAsync(model.Cipher.ToCipher(cipher), new Guid(model.Cipher.OrganizationId),
                model.SubvaultIds.Select(s => new Guid(s)), userId);
        }

        [HttpPut("{id}/subvaults")]
        [HttpPost("{id}/subvaults")]
        public async Task PutSubvaults(string id, [FromBody]CipherSubvaultsRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationUser(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveSubvaultsAsync(cipher, model.SubvaultIds.Select(s => new Guid(s)), userId, false);
        }

        [HttpPut("{id}/subvaults-admin")]
        [HttpPost("{id}/subvaults-admin")]
        public async Task PutSubvaultsAdmin(string id, [FromBody]CipherSubvaultsRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveSubvaultsAsync(cipher, model.SubvaultIds.Select(s => new Guid(s)), userId, true);
        }

        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task Delete(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null)
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
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAsync(cipher, userId, true);
        }
    }
}
