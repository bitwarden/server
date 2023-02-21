using System.Text.Json;
using Azure.Messaging.EventGrid;
using Bit.Api.Models.Request;
using Bit.Api.Models.Request.Accounts;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Core.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class CiphersController : Controller
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICipherService _cipherService;
    private readonly IUserService _userService;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly IProviderService _providerService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<CiphersController> _logger;
    private readonly GlobalSettings _globalSettings;

    public CiphersController(
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICipherService cipherService,
        IUserService userService,
        IAttachmentStorageService attachmentStorageService,
        IProviderService providerService,
        ICurrentContext currentContext,
        ILogger<CiphersController> logger,
        GlobalSettings globalSettings)
    {
        _cipherRepository = cipherRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _cipherService = cipherService;
        _userService = userService;
        _attachmentStorageService = attachmentStorageService;
        _providerService = providerService;
        _currentContext = currentContext;
        _logger = logger;
        _globalSettings = globalSettings;
    }

    [HttpGet("{id}")]
    public async Task<CipherResponseModel> Get(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        return new CipherResponseModel(cipher, _globalSettings);
    }

    [HttpGet("{id}/admin")]
    public async Task<CipherMiniResponseModel> GetAdmin(string id)
    {
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.ViewAllCollections(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpGet("{id}/full-details")]
    [HttpGet("{id}/details")]
    public async Task<CipherDetailsResponseModel> GetDetails(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipherId = new Guid(id);
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher == null)
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
        if (hasOrgs)
        {
            var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
            collectionCiphersGroupDict = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(s => s.Key);
        }

        var responses = ciphers.Select(c => new CipherDetailsResponseModel(c, _globalSettings,
            collectionCiphersGroupDict)).ToList();
        return new ListResponseModel<CipherDetailsResponseModel>(responses);
    }

    [HttpPost("")]
    public async Task<CipherResponseModel> Post([FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = model.ToCipherDetails(userId);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, userId, model.LastKnownRevisionDate, null, cipher.OrganizationId.HasValue);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPost("create")]
    public async Task<CipherResponseModel> PostCreate([FromBody] CipherCreateRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = model.Cipher.ToCipherDetails(userId);
        if (cipher.OrganizationId.HasValue && !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveDetailsAsync(cipher, userId, model.Cipher.LastKnownRevisionDate, model.CollectionIds, cipher.OrganizationId.HasValue);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPost("admin")]
    public async Task<CipherMiniResponseModel> PostAdmin([FromBody] CipherCreateRequestModel model)
    {
        var cipher = model.Cipher.ToOrganizationCipher();
        if (!await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SaveAsync(cipher, userId, model.Cipher.LastKnownRevisionDate, model.CollectionIds, true, false);

        var response = new CipherMiniResponseModel(cipher, _globalSettings, false);
        return response;
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<CipherResponseModel> Put(Guid id, [FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(id, userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var collectionIds = (await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id)).Select(c => c.CollectionId).ToList();
        var modelOrgId = string.IsNullOrWhiteSpace(model.OrganizationId) ?
            (Guid?)null : new Guid(model.OrganizationId);
        if (cipher.OrganizationId != modelOrgId)
        {
            throw new BadRequestException("Organization mismatch. Re-sync if you recently moved this item, " +
                "then try again.");
        }

        await _cipherService.SaveDetailsAsync(model.ToCipherDetails(cipher), userId, model.LastKnownRevisionDate, collectionIds);

        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/admin")]
    [HttpPost("{id}/admin")]
    public async Task<CipherMiniResponseModel> PutAdmin(Guid id, [FromBody] CipherRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(id);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        var collectionIds = (await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, id)).Select(c => c.CollectionId).ToList();
        // object cannot be a descendant of CipherDetails, so let's clone it.
        var cipherClone = model.ToCipher(cipher).Clone();
        await _cipherService.SaveAsync(cipherClone, userId, model.LastKnownRevisionDate, collectionIds, true, false);

        var response = new CipherMiniResponseModel(cipherClone, _globalSettings, cipher.OrganizationUseTotp);
        return response;
    }

    [HttpGet("organization-details")]
    public async Task<ListResponseModel<CipherMiniDetailsResponseModel>> GetOrganizationCollections(
        string organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgIdGuid = new Guid(organizationId);

        (IEnumerable<CipherOrganizationDetails> orgCiphers, Dictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphersGroupDict) = await _cipherService.GetOrganizationCiphers(userId, orgIdGuid);

        var responses = orgCiphers.Select(c => new CipherMiniDetailsResponseModel(c, _globalSettings,
            collectionCiphersGroupDict, c.OrganizationUseTotp));

        var providerId = await _currentContext.ProviderIdForOrg(orgIdGuid);
        if (providerId.HasValue)
        {
            await _providerService.LogProviderAccessToOrganizationAsync(orgIdGuid);
        }

        return new ListResponseModel<CipherMiniDetailsResponseModel>(responses);
    }

    [HttpPost("import")]
    public async Task PostImport([FromBody] ImportCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
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
    public async Task PostImport([FromQuery] string organizationId,
        [FromBody] ImportOrganizationCiphersRequestModel model)
    {
        if (!_globalSettings.SelfHosted &&
            (model.Ciphers.Count() > 6000 || model.CollectionRelationships.Count() > 12000 ||
                model.Collections.Count() > 1000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        var orgId = new Guid(organizationId);
        if (!await _currentContext.AccessImportExport(orgId))
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
    public async Task<CipherResponseModel> PutPartial(string id, [FromBody] CipherPartialRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var folderId = string.IsNullOrWhiteSpace(model.FolderId) ? null : (Guid?)new Guid(model.FolderId);
        var cipherId = new Guid(id);
        await _cipherRepository.UpdatePartialAsync(cipherId, userId, folderId, model.Favorite);

        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        var response = new CipherResponseModel(cipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/share")]
    [HttpPost("{id}/share")]
    public async Task<CipherResponseModel> PutShare(string id, [FromBody] CipherShareRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipherId = new Guid(id);
        var cipher = await _cipherRepository.GetByIdAsync(cipherId);
        if (cipher == null || cipher.UserId != userId ||
            !await _currentContext.OrganizationUser(new Guid(model.Cipher.OrganizationId)))
        {
            throw new NotFoundException();
        }

        var original = cipher.Clone();
        await _cipherService.ShareAsync(original, model.Cipher.ToCipher(cipher), new Guid(model.Cipher.OrganizationId),
            model.CollectionIds.Select(c => new Guid(c)), userId, model.Cipher.LastKnownRevisionDate);

        var sharedCipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        var response = new CipherResponseModel(sharedCipher, _globalSettings);
        return response;
    }

    [HttpPut("{id}/collections")]
    [HttpPost("{id}/collections")]
    public async Task PutCollections(string id, [FromBody] CipherCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.OrganizationUser(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SaveCollectionsAsync(cipher,
            model.CollectionIds.Select(c => new Guid(c)), userId, false);
    }

    [HttpPut("{id}/collections-admin")]
    [HttpPost("{id}/collections-admin")]
    public async Task PutCollectionsAdmin(string id, [FromBody] CipherCollectionsRequestModel model)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
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
        if (cipher == null)
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
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAsync(cipher, userId, true);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task DeleteMany([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time. " +
                "Consider using the \"Purge Vault\" option instead.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.DeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
    }

    [HttpDelete("admin")]
    [HttpPost("delete-admin")]
    public async Task DeleteManyAdmin([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time. " +
                "Consider using the \"Purge Vault\" option instead.");
        }

        if (model == null || string.IsNullOrWhiteSpace(model.OrganizationId) ||
            !await _currentContext.EditAnyCollection(new Guid(model.OrganizationId)))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.DeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId, new Guid(model.OrganizationId), true);
    }

    [HttpPut("{id}/delete")]
    public async Task PutDelete(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }
        await _cipherService.SoftDeleteAsync(cipher, userId);
    }

    [HttpPut("{id}/delete-admin")]
    public async Task PutDeleteAdmin(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.SoftDeleteAsync(cipher, userId, true);
    }

    [HttpPut("delete")]
    public async Task PutDeleteMany([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SoftDeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId);
    }

    [HttpPut("delete-admin")]
    public async Task PutDeleteManyAdmin([FromBody] CipherBulkDeleteRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only delete up to 500 items at a time.");
        }

        if (model == null || string.IsNullOrWhiteSpace(model.OrganizationId) ||
            !await _currentContext.EditAnyCollection(new Guid(model.OrganizationId)))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.SoftDeleteManyAsync(model.Ids.Select(i => new Guid(i)), userId, new Guid(model.OrganizationId), true);
    }

    [HttpPut("{id}/restore")]
    public async Task<CipherResponseModel> PutRestore(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(cipher, userId);
        return new CipherResponseModel(cipher, _globalSettings);
    }

    [HttpPut("{id}/restore-admin")]
    public async Task<CipherMiniResponseModel> PutRestoreAdmin(string id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(new Guid(id));
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.RestoreAsync(cipher, userId, true);
        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpPut("restore")]
    public async Task<ListResponseModel<CipherResponseModel>> PutRestoreMany([FromBody] CipherBulkRestoreRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only restore up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var cipherIdsToRestore = new HashSet<Guid>(model.Ids.Select(i => new Guid(i)));

        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId);
        var restoringCiphers = ciphers.Where(c => cipherIdsToRestore.Contains(c.Id) && c.Edit);

        await _cipherService.RestoreManyAsync(restoringCiphers, userId);
        var responses = restoringCiphers.Select(c => new CipherResponseModel(c, _globalSettings));
        return new ListResponseModel<CipherResponseModel>(responses);
    }

    [HttpPut("move")]
    [HttpPost("move")]
    public async Task MoveMany([FromBody] CipherBulkMoveRequestModel model)
    {
        if (!_globalSettings.SelfHosted && model.Ids.Count() > 500)
        {
            throw new BadRequestException("You can only move up to 500 items at a time.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        await _cipherService.MoveManyAsync(model.Ids.Select(i => new Guid(i)),
            string.IsNullOrWhiteSpace(model.FolderId) ? (Guid?)null : new Guid(model.FolderId), userId);
    }

    [HttpPut("share")]
    [HttpPost("share")]
    public async Task PutShareMany([FromBody] CipherBulkShareRequestModel model)
    {
        var organizationId = new Guid(model.Ciphers.First().OrganizationId);
        if (!await _currentContext.OrganizationUser(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var ciphers = await _cipherRepository.GetManyByUserIdAsync(userId, false);
        var ciphersDict = ciphers.ToDictionary(c => c.Id);

        var shareCiphers = new List<(Cipher, DateTime?)>();
        foreach (var cipher in model.Ciphers)
        {
            if (!ciphersDict.ContainsKey(cipher.Id.Value))
            {
                throw new BadRequestException("Trying to move ciphers that you do not own.");
            }

            shareCiphers.Add((cipher.ToCipher(ciphersDict[cipher.Id.Value]), cipher.LastKnownRevisionDate));
        }

        await _cipherService.ShareManyAsync(shareCiphers, organizationId,
            model.CollectionIds.Select(c => new Guid(c)), userId);
    }

    [HttpPost("purge")]
    public async Task PostPurge([FromBody] SecretVerificationRequestModel model, string organizationId = null)
    {
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        if (!await _userService.VerifySecretAsync(user, model.Secret))
        {
            ModelState.AddModelError(string.Empty, "User verification failed.");
            await Task.Delay(2000);
            throw new BadRequestException(ModelState);
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            await _cipherRepository.DeleteByUserIdAsync(user.Id);
        }
        else
        {
            var orgId = new Guid(organizationId);
            if (!await _currentContext.EditAnyCollection(orgId))
            {
                throw new NotFoundException();
            }
            await _cipherService.PurgeAsync(orgId);
        }
    }

    [HttpPost("{id}/attachment/v2")]
    public async Task<AttachmentUploadDataResponseModel> PostAttachment(string id, [FromBody] AttachmentRequestModel request)
    {
        var idGuid = new Guid(id);
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = request.AdminRequest ?
            await _cipherRepository.GetOrganizationDetailsByIdAsync(idGuid) :
            await _cipherRepository.GetByIdAsync(idGuid, userId);

        if (cipher == null || (request.AdminRequest && (!cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))))
        {
            throw new NotFoundException();
        }

        if (request.FileSize > CipherService.MAX_FILE_SIZE)
        {
            throw new BadRequestException($"Max file size is {CipherService.MAX_FILE_SIZE_READABLE}.");
        }

        var (attachmentId, uploadUrl) = await _cipherService.CreateAttachmentForDelayedUploadAsync(cipher,
            request.Key, request.FileName, request.FileSize, request.AdminRequest, userId);
        return new AttachmentUploadDataResponseModel
        {
            AttachmentId = attachmentId,
            Url = uploadUrl,
            FileUploadType = _attachmentStorageService.FileUploadType,
            CipherResponse = request.AdminRequest ? null : new CipherResponseModel((CipherDetails)cipher, _globalSettings),
            CipherMiniResponse = request.AdminRequest ? new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp) : null,
        };
    }

    [HttpGet("{id}/attachment/{attachmentId}/renew")]
    public async Task<AttachmentUploadDataResponseModel> RenewFileUploadUrl(string id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipherId = new Guid(id);
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        var attachments = cipher?.GetAttachments();

        if (attachments == null || !attachments.ContainsKey(attachmentId) || attachments[attachmentId].Validated)
        {
            throw new NotFoundException();
        }

        return new AttachmentUploadDataResponseModel
        {
            Url = await _attachmentStorageService.GetAttachmentUploadUrlAsync(cipher, attachments[attachmentId]),
            FileUploadType = _attachmentStorageService.FileUploadType,
        };
    }

    [HttpPost("{id}/attachment/{attachmentId}")]
    [SelfHosted(SelfHostedOnly = true)]
    [RequestSizeLimit(Constants.FileSize501mb)]
    [DisableFormValueModelBinding]
    public async Task PostFileForExistingAttachment(string id, string attachmentId)
    {
        if (!Request?.ContentType.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        var attachments = cipher?.GetAttachments();
        if (attachments == null || !attachments.ContainsKey(attachmentId))
        {
            throw new NotFoundException();
        }
        var attachmentData = attachments[attachmentId];

        await Request.GetFileAsync(async (stream) =>
        {
            await _cipherService.UploadFileForExistingAttachmentAsync(stream, cipher, attachmentData);
        });
    }

    [HttpPost("{id}/attachment")]
    [Obsolete("Deprecated Attachments API", false)]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task<CipherResponseModel> PostAttachment(string id)
    {
        ValidateAttachment();

        var idGuid = new Guid(id);
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(idGuid, userId);
        if (cipher == null)
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
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task<CipherMiniResponseModel> PostAttachmentAdmin(string id)
    {
        ValidateAttachment();

        var idGuid = new Guid(id);
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetOrganizationDetailsByIdAsync(idGuid);
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await Request.GetFileAsync(async (stream, fileName, key) =>
        {
            await _cipherService.CreateAttachmentAsync(cipher, stream, fileName, key,
                    Request.ContentLength.GetValueOrDefault(0), userId, true);
        });

        return new CipherMiniResponseModel(cipher, _globalSettings, cipher.OrganizationUseTotp);
    }

    [HttpGet("{id}/attachment/{attachmentId}")]
    public async Task<AttachmentResponseModel> GetAttachmentData(string id, string attachmentId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id), userId);
        var result = await _cipherService.GetAttachmentDownloadDataAsync(cipher, attachmentId);
        return new AttachmentResponseModel(result);
    }

    [HttpPost("{id}/attachment/{attachmentId}/share")]
    [RequestSizeLimit(Constants.FileSize101mb)]
    [DisableFormValueModelBinding]
    public async Task PostAttachmentShare(string id, string attachmentId, Guid organizationId)
    {
        ValidateAttachment();

        var userId = _userService.GetProperUserId(User).Value;
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null || cipher.UserId != userId || !await _currentContext.OrganizationUser(organizationId))
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
        if (cipher == null)
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
        if (cipher == null || !cipher.OrganizationId.HasValue ||
            !await _currentContext.EditAnyCollection(cipher.OrganizationId.Value))
        {
            throw new NotFoundException();
        }

        await _cipherService.DeleteAttachmentAsync(cipher, attachmentId, userId, true);
    }

    [AllowAnonymous]
    [HttpPost("attachment/validate/azure")]
    public async Task<ObjectResult> AzureValidateFile()
    {
        return await ApiHelpers.HandleAzureEvents(Request, new Dictionary<string, Func<EventGridEvent, Task>>
        {
            {
                "Microsoft.Storage.BlobCreated", async (eventGridEvent) =>
                {
                    try
                    {
                        var blobName = eventGridEvent.Subject.Split($"{AzureAttachmentStorageService.EventGridEnabledContainerName}/blobs/")[1];
                        var (cipherId, organizationId, attachmentId) = AzureAttachmentStorageService.IdentifiersFromBlobName(blobName);
                        var cipher = await _cipherRepository.GetByIdAsync(new Guid(cipherId));
                        var attachments = cipher?.GetAttachments() ?? new Dictionary<string, CipherAttachment.MetaData>();

                        if (cipher == null || !attachments.ContainsKey(attachmentId) || attachments[attachmentId].Validated)
                        {
                            if (_attachmentStorageService is AzureSendFileStorageService azureFileStorageService)
                            {
                                await azureFileStorageService.DeleteBlobAsync(blobName);
                            }

                            return;
                        }

                        await _cipherService.ValidateCipherAttachmentFile(cipher, attachments[attachmentId]);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Uncaught exception occurred while handling event grid event: {JsonSerializer.Serialize(eventGridEvent)}");
                        return;
                    }
                }
            }
        });
    }

    private void ValidateAttachment()
    {
        if (!Request?.ContentType.Contains("multipart/") ?? true)
        {
            throw new BadRequestException("Invalid content.");
        }
    }
}
