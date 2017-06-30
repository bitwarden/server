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
using Bit.Api.Utilities;
using Bit.Core.Models.Data;
using Newtonsoft.Json;

namespace Bit.Api.Controllers
{
    [Route("ciphers")]
    [Authorize("Application")]
    public class CiphersController : Controller
    {
        private readonly ICipherRepository _cipherRepository;
        private readonly ICollectionCipherRepository _collectionCipherRepository;
        private readonly ICipherService _cipherService;
        private readonly IUserService _userService;
        private readonly IAttachmentStorageService _attachmentStorageService;
        private readonly CurrentContext _currentContext;

        public CiphersController(
            ICipherRepository cipherRepository,
            ICollectionCipherRepository collectionCipherRepository,
            ICipherService cipherService,
            IUserService userService,
            IAttachmentStorageService attachmentStorageService,
            CurrentContext currentContext)
        {
            _cipherRepository = cipherRepository;
            _collectionCipherRepository = collectionCipherRepository;
            _cipherService = cipherService;
            _userService = userService;
            _attachmentStorageService = attachmentStorageService;
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
        [HttpGet("{id}/details")]
        public async Task<CipherDetailsResponseModel> GetDetails(string id)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipherId = new Guid(id);
            var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, cipherId);
            return new CipherDetailsResponseModel(cipher, collectionCiphers);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CipherResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId);
            var responses = ciphers.Select(c => new CipherResponseModel(c)).ToList();
            return new ListResponseModel<CipherResponseModel>(responses);
        }

        [HttpGet("details")]
        public async Task<ListResponseModel<CipherDetailsResponseModel>> GetCollections()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdHasCollectionsAsync(userId);

            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
            var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

            var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, collectionCiphersGroupDict));
            return new ListResponseModel<CipherDetailsResponseModel>(responses);
        }

        [HttpGet("organization-details")]
        public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCollections(string organizationId)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var orgIdGuid = new Guid(organizationId);
            if(!_currentContext.OrganizationAdmin(orgIdGuid))
            {
                throw new NotFoundException();
            }

            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(orgIdGuid);

            var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(orgIdGuid);
            var collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);

            var responses = ciphers.Select(c => new CipherMiniDetailsResponseModel(c, collectionCiphersGroupDict));
            return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
        }

        [HttpPost("import")]
        public async Task PostImport([FromBody]ImportPasswordsRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
            var ciphers = model.Logins.Select(l => l.ToCipherDetails(userId)).ToList();
            await _cipherService.ImportCiphersAsync(folders, ciphers, model.FolderRelationships);
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
                model.CollectionIds.Select(c => new Guid(c)), userId);
        }

        [HttpPut("{id}/collections")]
        [HttpPost("{id}/collections")]
        public async Task PutCollections(string id, [FromBody]CipherCollectionsRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationUser(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveCollectionsAsync(cipher, model.CollectionIds.Select(c => new Guid(c)), userId, false);
        }

        [HttpPut("{id}/collections-admin")]
        [HttpPost("{id}/collections-admin")]
        public async Task PutCollectionsAdmin(string id, [FromBody]CipherCollectionsRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveCollectionsAsync(cipher, model.CollectionIds.Select(c => new Guid(c)), userId, true);
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

        [HttpDelete("")]
        [HttpPost("delete")]
        public async Task DeleteMany([FromBody]CipherBulkDeleteRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.DeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
        }

        [HttpPut("move")]
        [HttpPost("move")]
        public async Task MoveMany([FromBody]CipherBulkMoveRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.MoveManyAsync(model.Ids.Select(i => new Guid(i)),
                string.IsNullOrWhiteSpace(model.FolderId) ? (Guid?)null : new Guid(model.FolderId), userId);
        }

        [HttpPost("{id}/attachment")]
        [DisableFormValueModelBinding]
        public async Task PostAttachment(string id)
        {
            if(!Request?.ContentType.Contains("multipart/") ?? true)
            {
                throw new BadRequestException("Invalid content.");
            }

            var idGuid = new Guid(id);
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(idGuid, userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            await Request.GetFileAsync(async (stream, fileName) =>
            {
                await _cipherService.AttachAsync(cipher, stream, fileName, Request.ContentLength.GetValueOrDefault(0), userId);
            });
        }

        [HttpDelete("{id}/attachment/{attachmentId}")]
        [HttpPost("{id}/attachment/{attachmentId}/delete")]
        public async Task DeleteAttachment(string id, string attachmentId)
        {
            var idGuid = new Guid(id);
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(idGuid, userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            // TODO: check and remove attachmentId from cipher in database

            var storedFilename = $"{idGuid}/{attachmentId}";
            await _attachmentStorageService.DeleteAttachmentAsync(storedFilename);
        }
    }
}
