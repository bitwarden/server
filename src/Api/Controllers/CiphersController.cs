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
using System.Collections.Generic;
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
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public CiphersController(
            ICipherRepository cipherRepository,
            ICollectionCipherRepository collectionCipherRepository,
            ICipherService cipherService,
            IUserService userService,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _cipherRepository = cipherRepository;
            _collectionCipherRepository = collectionCipherRepository;
            _cipherService = cipherService;
            _userService = userService;
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
        public async Task<ListResponseModel<CipherDetailsResponseModel>> Get()
        {
            var userId = _userService.GetProperUserId(User).Value;
            var hasOrgs = _currentContext.Organizations?.Any() ?? false;
            // TODO: Use hasOrgs proper for cipher listing here?
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, true || hasOrgs);
            Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict = null;
            if(hasOrgs)
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
            if(cipher.OrganizationId.HasValue && !_currentContext.OrganizationUser(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveDetailsAsync(cipher, userId, null, cipher.OrganizationId.HasValue);
            var response = new CipherResponseModel(cipher, _globalSettings);
            return response;
        }

        [HttpPost("create")]
        public async Task<CipherResponseModel> PostCreate([FromBody]CipherCreateRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipher = model.Cipher.ToCipherDetails(userId);
            if(cipher.OrganizationId.HasValue && !_currentContext.OrganizationUser(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            await _cipherService.SaveDetailsAsync(cipher, userId, model.CollectionIds, cipher.OrganizationId.HasValue);
            var response = new CipherResponseModel(cipher, _globalSettings);
            return response;
        }

        [HttpPost("admin")]
        public async Task<CipherMiniResponseModel> PostAdmin([FromBody]CipherCreateRequestModel model)
        {
            var cipher = model.Cipher.ToOrganizationCipher();
            if(!_currentContext.OrganizationAdmin(cipher.OrganizationId.Value))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            await _cipherService.SaveAsync(cipher, userId, model.CollectionIds, true, false);

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

            var modelOrgId = string.IsNullOrWhiteSpace(model.OrganizationId) ? 
                (Guid?)null : new Guid(model.OrganizationId);
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
            await _cipherService.SaveAsync(cipherClone, userId, null, true, false);

            var response = new CipherMiniResponseModel(cipherClone, _globalSettings, cipher.OrganizationUseTotp);
            return response;
        }

        [HttpGet("organization-details")]
        public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCollections(
            string organizationId)
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
            if(!_globalSettings.SelfHosted &&
                (model.Ciphers.Count() > 6000 || model.FolderRelationships.Count() > 6000 ||
                    model.Folders.Count() > 1000))
            {
                throw new BadRequestException("You cannot import this much data at once.");
            }

            var userId = _userService.GetProperUserId(User).Value;
            var folders = model.Folders.Select(f => f.ToFolder(userId)).ToList();
            var ciphers = model.Ciphers.Select(c => c.ToCipherDetails(userId, false)).ToList();
            await _cipherService.ImportCiphersAsync(folders, ciphers, model.FolderRelationships);
        }

        [HttpPost("import-organization")]
        public async Task PostImport([FromQuery]string organizationId,
            [FromBody]ImportOrganizationCiphersRequestModel model)
        {
            if(!_globalSettings.SelfHosted &&
                (model.Ciphers.Count() > 6000 || model.CollectionRelationships.Count() > 12000 ||
                    model.Collections.Count() > 1000))
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
        public async Task<CipherResponseModel> PutShare(string id, [FromBody]CipherShareRequestModel model)
        {
            var userId = _userService.GetProperUserId(User).Value;
            var cipherId = new Guid(id);
            var cipher = await _cipherRepository.GetByIdAsync(cipherId);
            if(cipher == null || cipher.UserId != userId ||
                !_currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
            {
                throw new NotFoundException();
            }

            var original = CoreHelpers.CloneObject(cipher);
            await _cipherService.ShareAsync(original, model.Cipher.ToCipher(cipher), 
                new Guid(model.Cipher.OrganizationId), model.CollectionIds.Select(c => new Guid(c)), userId);

            var sharedCipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
            var response = new CipherResponseModel(sharedCipher, _globalSettings);
            return response;
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

            await _cipherService.SaveCollectionsAsync(cipher, 
                model.CollectionIds.Select(c => new Guid(c)), userId, false);
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

            await _cipherService.SaveCollectionsAsync(cipher, 
                model.CollectionIds.Select(c => new Guid(c)), userId, true);
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

        [HttpPut("share")]
        [HttpPost("share")]
        public async Task PutShareMany([FromBody]CipherBulkShareRequestModel model)
        {
            var organizationId = new Guid(model.Ciphers.First().OrganizationId);
            if(!_currentContext.OrganizationUser(organizationId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User).Value;
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, false);
            var ciphersDict = ciphers.ToDictionary(c => c.Id);

            var shareCiphers = new List<Cipher>();
            foreach(var cipher in model.Ciphers)
            {
                if(!ciphersDict.ContainsKey(cipher.Id.Value))
                {
                    throw new BadRequestException("Trying to share ciphers that you do not own.");
                }

                shareCiphers.Add(cipher.ToCipher(ciphersDict[cipher.Id.Value]));
            }

            await _cipherService.ShareManyAsync(shareCiphers, organizationId,
                model.CollectionIds.Select(c => new Guid(c)), userId);
        }

        [HttpPost("purge")]
        public async Task PostPurge([FromBody]CipherPurgeRequestModel model, string organizationId = null)
        {
            var user = await _userService.GetUserByPrincipalAsync(User);
            if(user == null)
            {
                throw new UnauthorizedAccessException();
            }

            if(!await _userService.CheckPasswordAsync(user, model.MasterPasswordHash))
            {
                ModelState.AddModelError("MasterPasswordHash", "Invalid password.");
                await Task.Delay(2000);
                throw new BadRequestException(ModelState);
            }

            if(string.IsNullOrWhiteSpace(organizationId))
            {
                await _cipherRepository.DeleteByUserIdAsync(user.Id);
            }
            else
            {
                var orgId = new Guid(organizationId);
                if(!_currentContext.OrganizationAdmin(orgId))
                {
                    throw new NotFoundException();
                }
                await _cipherService.PurgeAsync(orgId);
            }
        }

        [HttpPost("{id}/attachment")]
        [RequestSizeLimit(105_906_176)]
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

            await Request.GetFileAsync(async (stream, fileName, key) =>
            {
                await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                        Request.ContentLength.GetValueOrDefault(0), userId);
            });

            return new CipherResponseModel(cipher, _globalSettings);
        }

        [HttpPost("{id}/attachment-admin")]
        [RequestSizeLimit(105_906_176)]
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

            await Request.GetFileAsync(async (stream, fileName, key) =>
            {
                await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                        Request.ContentLength.GetValueOrDefault(0), userId, true);
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

            await Request.GetFileAsync(async (stream, fileName, key) =>
            {
                await _cipherService.CreateAttachmentShareAsync(cipher, stream,
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

            await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, true);
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
