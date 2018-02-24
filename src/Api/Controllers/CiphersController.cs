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
using Bit.Core.Utilities;
using Core.Models.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;

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
        private readonly UserManager<User> _userManager;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public CiphersController(
            ICipherRepository cipherRepository,
            ICollectionCipherRepository collectionCipherRepository,
            ICipherService cipherService,
            IUserService userService,
            UserManager<User> userManager,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _cipherRepository = cipherRepository;
            _collectionCipherRepository = collectionCipherRepository;
            _cipherService = cipherService;
            _userService = userService;
            _userManager = userManager;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
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

            return new CipherResponseModel(cipher, _globalSettings);
        }

        [HttpGet("{id}/admin")]
        public async Task<CipherResponseModel> GetAdmin(string id)
        {
            var cipher = await _cipherRepository.GetDetailsByIdAsync(new Guid(id));
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            return new CipherResponseModel(cipher, _globalSettings);
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
            return new CipherDetailsResponseModel(cipher, _globalSettings, collectionCiphers);
        }

        [HttpGet("")]
        public async Task<ListResponseModel<CipherDetailsResponseModel>> Get([FromQuery]Core.Enums.CipherType? type = null)
        {
            var userId = _userService.GetProperUserId(User).Value;

            IEnumerable<CipherDetails> ciphers;
            if(type.HasValue)
            {
                ciphers = await _cipherRepository.GetManyByTypeAndUserIdAsync(type.Value, userId);
            }
            else
            {
                ciphers = await _cipherRepository.GetManyByUserIdAsync(userId);
            }

            Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
            if(_currentContext.Organizations.Any())
            {
                var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
                collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
            }

            var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, _globalSettings,
                collectionCiphersGroupDict)).ToList();
            return new ListResponseModel<CipherDetailsResponseModel>(responses);
        }

        [HttpPost("")]
        public async Task<CipherResponseModel> Post([FromBody]CipherRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = model.ToCipherDetails(userId);
            await _cipherService.SaveDetailsAsync(cipher, userId);

            var response = new CipherResponseModel(cipher, _globalSettings);
            return response;
        }

        [HttpPost("admin")]
        public async Task<CipherMiniResponseModel> PostAdmin([FromBody]CipherRequestModel model)
        {
            var cipher = model.ToOrganizationCipher();
            if(!_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.SaveAsync(cipher, userId, true);

            var response = new CipherMiniResponseModel(cipher, _globalSettings, false);
            return response;
        }

        [HttpPut("{id}")]
        [HttpPost("{id}")]
        public async Task<CipherResponseModel> Put(string id, [FromBody]CipherRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            var modelOrgId = string.IsNullOrWhiteSpace(model.OrganizationId) ? (Guid?)null : new Guid(model.OrganizationId);
            if(cipher.OrganizationId != modelOrgId)
            {
                throw new BadRequestException("Organization mismatch. Re-sync if you recently shared this item, " +
                    "then try again.");
            }

            await _cipherService.SaveDetailsAsync(model.ToCipherDetails(cipher), userId);

            var response = new CipherResponseModel(cipher, _globalSettings);
            return response;
        }

        [HttpPut("{id}/admin")]
        [HttpPost("{id}/admin")]
        public async Task<CipherMiniResponseModel> PutAdmin(string id, [FromBody]CipherRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetDetailsByIdAsync(new Guid(id));
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            // object cannot be a descendant of CipherDetails, so let's clone it.
            var cipherClone = CoreHelpers.CloneObject(model.ToCipher(cipher));
            await _cipherService.SaveAsync(cipherClone, userId, true);

            var response = new CipherMiniResponseModel(cipherClone, _globalSettings, cipher.OrganizationUseTotp);
            return response;
        }

        [Obsolete]
        [HttpGet("details")]
        public async Task<ListResponseModel<CipherDetailsResponseModel>> GetCollections()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdHasCollectionsAsync(userId);

            Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
            if(_currentContext.Organizations.Any())
            {
                var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
                collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
            }

            var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, _globalSettings, collectionCiphersGroupDict));
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

            var responses = ciphers.Select(c => new CipherMiniDetailsResponseModel(c, _globalSettings,
                collectionCiphersGroupDict));
            return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
        }

        [HttpPost("import")]
        public async Task PostImport([FromBody]ImportCiphersRequestModel model)
        {
            if(model.Ciphers.Count() > 5000 || model.FolderRelationships.Count() > 5000 || model.Folders.Count() > 200)
            {
                throw new BadRequestException("You cannot import this much data at once.");
            }

            var userId = _userService.GetProperUserId(User).Value;
            var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
            var ciphers = model.Ciphers.Select(c => c.ToCipherDetails(userId)).ToList();
            await _cipherService.ImportCiphersAsync(folders, ciphers, model.FolderRelationships);
        }

        [HttpPost("import-organization")]
        public async Task PostImport([FromQuery]string organizationId, [FromBody]ImportOrganizationCiphersRequestModel model)
        {
            if(model.Ciphers.Count() > 5000 || model.CollectionRelationships.Count() > 5000 || model.Collections.Count() > 200)
            {
                throw new BadRequestException("You cannot import this much data at once.");
            }

            var orgId = new Guid(organizationId);
            if(!_currentContext.OrganizationAdmin(orgId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            var collections = model.Collections.Select(c => c.ToCollection(orgId)).ToList();
            var ciphers = model.Ciphers.Select(l => l.ToOrganizationCipherDetails(orgId)).ToList();
            await _cipherService.ImportCiphersAsync(collections, ciphers, model.CollectionRelationships, userId);
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
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(cipher == null || cipher.UserId != userId ||
                !_currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
            {
                throw new NotFoundException();
            }

            var original = CoreHelpers.CloneObject(cipher);
            await _cipherService.ShareAsync(original, model.Cipher.ToCipher(cipher), new Guid(model.Cipher.OrganizationId),
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
            if(!_globalSettings.SelfHosted && model.Ids.Count() > 500)
            {
                throw new BadRequestException("You can only delete up to 500 items at a time. " +
                    "Consider using the \"Purge Vault\" option instead.");
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.DeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
        }

        [HttpPut("move")]
        [HttpPost("move")]
        public async Task MoveMany([FromBody]CipherBulkMoveRequestModel model)
        {
            if(!_globalSettings.SelfHosted && model.Ids.Count() > 500)
            {
                throw new BadRequestException("You can only move up to 500 items at a time.");
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.MoveManyAsync(model.Ids.Select(i => new Guid(i)),
                string.IsNullOrWhiteSpace(model.FolderId) ? (Guid?)null : new Guid(model.FolderId), userId);
        }

        [HttpPost("purge")]
        public async Task PostPurge([FromBody]CipherPurgeRequestModel model)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userManager.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                ModelState.AddModelError("MasterPasswordHash", "Invalid password.");
                await Task.Delay(2000);
                throw new BadRequestException(ModelState);
            }

            await _cipherRepository.DeleteByUserIdAsync(user.Id);
        }

        [HttpPost("{id}/attachment")]
        [DisableFormValueModelBinding]
        public async Task<CipherResponseModel> PostAttachment(string id)
        {
            ValidateAttachment();

            var idGuid = new Guid(id);
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(idGuid, userId);
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            await Request.GetFileAsync(async (stream, fileName) =>
            {
                await _cipherService.CreateAttachmentAsync(cipher, stream, fileName,
                        Request.ContentLength.GetValueOrDefault(0), userId);
            });

            return new CipherResponseModel(cipher, _globalSettings);
        }

        [HttpPost("{id}/attachment-admin")]
        [DisableFormValueModelBinding]
        public async Task<CipherResponseModel> PostAttachmentAdmin(string id)
        {
            ValidateAttachment();

            var idGuid = new Guid(id);
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetDetailsByIdAsync(idGuid);
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await Request.GetFileAsync(async (stream, fileName) =>
            {
                await _cipherService.CreateAttachmentAsync(cipher, stream, fileName,
                        Request.ContentLength.GetValueOrDefault(0), userId);
            });

            return new CipherResponseModel(cipher, _globalSettings);
        }

        [HttpPost("{id}/attachment/{attachmentId}/share")]
        [RequestSizeLimit(105_906_176)]
        [DisableFormValueModelBinding]
        public async Task PostAttachmentShare(string id, string attachmentId, Guid organizationId)
        {
            ValidateAttachment();

            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(cipher == null || cipher.UserId != userId || !_currentContext.OrganizationUser(organizationId))
            {
                throw new NotFoundException();
            }

            await Request.GetFileAsync(async (stream, fileName) =>
            {
                await _cipherService.CreateAttachmentShareAsync(cipher, stream, fileName,
                    Request.ContentLength.GetValueOrDefault(0), attachmentId, organizationId);
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

            await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, false);
        }

        [HttpDelete("{id}/attachment/{attachmentId}/admin")]
        [HttpPost("{id}/attachment/{attachmentId}/delete-admin")]
        public async Task DeleteAttachmentAdmin(string id, string attachmentId)
        {
            var idGuid = new Guid(id);
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = await _cipherRepository.GetByIdAsync(idGuid);
            if(cipher == null || !cipher.OrganizationId.HasValue ||
                !_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, false);
        }

        private void ValidateAttachment()
        {
            if(!Request?.ContentType.Contains("multipart/") ?? true)
            {
                throw new BadRequestException("Invalid content.");
            }

            if(Request.ContentLength > 105906176) // 101 MB, give em' 1 extra MB for cushion
            {
                throw new BadRequestException("Max file size is 100 MB.");
            }
        }
    }
}
