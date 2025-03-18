﻿using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Services;

public class CipherService : ICipherService
{
    public const long MAX_FILE_SIZE = Constants.FileSize501mb;
    public const string MAX_FILE_SIZE_READABLE = "500 MB";
    private readonly ICipherRepository _cipherRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly IEventService _eventService;
    private readonly IUserService _userService;
    private readonly IPolicyService _policyService;
    private readonly GlobalSettings _globalSettings;
    private const long _fileSizeLeeway = 1024L * 1024L; // 1MB
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IGetCipherPermissionsForUserQuery _getCipherPermissionsForUserQuery;

    public CipherService(
        ICipherRepository cipherRepository,
        IFolderRepository folderRepository,
        ICollectionRepository collectionRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionCipherRepository collectionCipherRepository,
        IPushNotificationService pushService,
        IAttachmentStorageService attachmentStorageService,
        IEventService eventService,
        IUserService userService,
        IPolicyService policyService,
        GlobalSettings globalSettings,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IGetCipherPermissionsForUserQuery getCipherPermissionsForUserQuery)
    {
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _collectionRepository = collectionRepository;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _pushService = pushService;
        _attachmentStorageService = attachmentStorageService;
        _eventService = eventService;
        _userService = userService;
        _policyService = policyService;
        _globalSettings = globalSettings;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _getCipherPermissionsForUserQuery = getCipherPermissionsForUserQuery;
    }

    public async Task SaveAsync(Cipher cipher, Guid savingUserId, DateTime? lastKnownRevisionDate,
         IEnumerable<Guid> collectionIds = null, bool skipPermissionCheck = false, bool limitCollectionScope = true)
    {
        if (!skipPermissionCheck && !(await UserCanEditAsync(cipher, savingUserId)))
        {
            throw new BadRequestException("You do not have permissions to edit this.");
        }

        if (cipher.Id == default(Guid))
        {
            if (cipher.OrganizationId.HasValue && collectionIds != null)
            {
                if (limitCollectionScope)
                {
                    // Set user ID to limit scope of collection ids in the create sproc
                    cipher.UserId = savingUserId;
                }
                await _cipherRepository.CreateAsync(cipher, collectionIds);

                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.CipherCreated, await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value), _currentContext));
            }
            else
            {
                await _cipherRepository.CreateAsync(cipher);
            }
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_Created);

            // push
            await _pushService.PushSyncCipherCreateAsync(cipher, null);
        }
        else
        {
            ValidateCipherLastKnownRevisionDateAsync(cipher, lastKnownRevisionDate);
            cipher.RevisionDate = DateTime.UtcNow;
            await _cipherRepository.ReplaceAsync(cipher);
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_Updated);

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
        }
    }

    public async Task SaveDetailsAsync(CipherDetails cipher, Guid savingUserId, DateTime? lastKnownRevisionDate,
        IEnumerable<Guid> collectionIds = null, bool skipPermissionCheck = false)
    {
        if (!skipPermissionCheck && !(await UserCanEditAsync(cipher, savingUserId)))
        {
            throw new BadRequestException("You do not have permissions to edit this.");
        }

        cipher.UserId = savingUserId;
        if (cipher.Id == default(Guid))
        {
            if (cipher.OrganizationId.HasValue && collectionIds != null)
            {
                var existingCollectionIds = (await _collectionRepository.GetManyByOrganizationIdAsync(cipher.OrganizationId.Value)).Select(c => c.Id);
                if (collectionIds.Except(existingCollectionIds).Any())
                {
                    throw new BadRequestException("Specified CollectionId does not exist on the specified Organization.");
                }
                await _cipherRepository.CreateAsync(cipher, collectionIds);
            }
            else
            {
                // Make sure the user can save new ciphers to their personal vault
                var anyPersonalOwnershipPolicies = await _policyService.AnyPoliciesApplicableToUserAsync(savingUserId, PolicyType.PersonalOwnership);
                if (anyPersonalOwnershipPolicies)
                {
                    throw new BadRequestException("Due to an Enterprise Policy, you are restricted from saving items to your personal vault.");
                }
                await _cipherRepository.CreateAsync(cipher);
            }
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_Created);

            if (cipher.OrganizationId.HasValue)
            {
                var org = await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value);
                cipher.OrganizationUseTotp = org.UseTotp;
            }

            // push
            await _pushService.PushSyncCipherCreateAsync(cipher, null);
        }
        else
        {
            ValidateCipherLastKnownRevisionDateAsync(cipher, lastKnownRevisionDate);
            cipher.RevisionDate = DateTime.UtcNow;
            await ValidateViewPasswordUserAsync(cipher);
            await _cipherRepository.ReplaceAsync(cipher);
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_Updated);

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
        }
    }

    public async Task UploadFileForExistingAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachment)
    {
        if (attachment == null)
        {
            throw new BadRequestException("Cipher attachment does not exist");
        }

        await _attachmentStorageService.UploadNewAttachmentAsync(stream, cipher, attachment);

        if (!await ValidateCipherAttachmentFile(cipher, attachment))
        {
            throw new BadRequestException("File received does not match expected file length.");
        }
    }

    public async Task<(string attachmentId, string uploadUrl)> CreateAttachmentForDelayedUploadAsync(Cipher cipher,
        string key, string fileName, long fileSize, bool adminRequest, Guid savingUserId)
    {
        await ValidateCipherEditForAttachmentAsync(cipher, savingUserId, adminRequest, fileSize);

        var attachmentId = Utilities.CoreHelpers.SecureRandomString(32, upper: false, special: false);
        var data = new CipherAttachment.MetaData
        {
            AttachmentId = attachmentId,
            FileName = fileName,
            Key = key,
            Size = fileSize,
            Validated = false,
        };

        var uploadUrl = await _attachmentStorageService.GetAttachmentUploadUrlAsync(cipher, data);

        await _cipherRepository.UpdateAttachmentAsync(new CipherAttachment
        {
            Id = cipher.Id,
            UserId = cipher.UserId,
            OrganizationId = cipher.OrganizationId,
            AttachmentId = attachmentId,
            AttachmentData = JsonSerializer.Serialize(data)
        });
        cipher.AddAttachment(attachmentId, data);

        // Update the revision date when an attachment is added
        cipher.RevisionDate = DateTime.UtcNow;
        await _cipherRepository.ReplaceAsync((CipherDetails)cipher);

        await _pushService.PushSyncCipherUpdateAsync(cipher, null);

        return (attachmentId, uploadUrl);
    }

    public async Task CreateAttachmentAsync(Cipher cipher, Stream stream, string fileName, string key,
        long requestLength, Guid savingUserId, bool orgAdmin = false)
    {
        await ValidateCipherEditForAttachmentAsync(cipher, savingUserId, orgAdmin, requestLength);

        var attachmentId = Utilities.CoreHelpers.SecureRandomString(32, upper: false, special: false);
        var data = new CipherAttachment.MetaData
        {
            AttachmentId = attachmentId,
            FileName = fileName,
            Key = key,
        };

        await _attachmentStorageService.UploadNewAttachmentAsync(stream, cipher, data);
        // Must read stream length after it has been saved, otherwise it's 0
        data.Size = stream.Length;

        try
        {
            var attachment = new CipherAttachment
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                AttachmentId = attachmentId,
                AttachmentData = JsonSerializer.Serialize(data)
            };

            await _cipherRepository.UpdateAttachmentAsync(attachment);
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_AttachmentCreated);
            cipher.AddAttachment(attachmentId, data);

            if (!await ValidateCipherAttachmentFile(cipher, data))
            {
                throw new Exception("Content-Length does not match uploaded file size");
            }
        }
        catch
        {
            // Clean up since this is not transactional
            await _attachmentStorageService.DeleteAttachmentAsync(cipher.Id, data);
            throw;
        }

        // Update the revision date when an attachment is added
        cipher.RevisionDate = DateTime.UtcNow;
        await _cipherRepository.ReplaceAsync((CipherDetails)cipher);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, null);
    }

    public async Task CreateAttachmentShareAsync(Cipher cipher, Stream stream, string fileName, string key,
        long requestLength, string attachmentId, Guid organizationId)
    {
        try
        {
            if (requestLength < 1)
            {
                throw new BadRequestException("No data to attach.");
            }

            if (cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if (cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Cipher belongs to an organization already.");
            }

            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if (org == null || !org.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("This organization cannot use attachments.");
            }

            var storageBytesRemaining = org.StorageBytesRemaining();
            if (storageBytesRemaining < requestLength)
            {
                throw new BadRequestException("Not enough storage available for this organization.");
            }

            var attachments = cipher.GetAttachments();
            if (!attachments.ContainsKey(attachmentId))
            {
                throw new BadRequestException($"Cipher does not own specified attachment");
            }

            var originalAttachmentMetadata = attachments[attachmentId];

            if (originalAttachmentMetadata.TempMetadata != null)
            {
                throw new BadRequestException("Another process is trying to migrate this attachment");
            }

            // Clone metadata to be modified and saved into the TempMetadata,
            // we cannot change the metadata here directly because if the subsequent endpoint fails
            // to be called, then the metadata would stay corrupted.
            var attachmentMetadata = CoreHelpers.CloneObject(originalAttachmentMetadata);
            attachmentMetadata.AttachmentId = originalAttachmentMetadata.AttachmentId;
            originalAttachmentMetadata.TempMetadata = attachmentMetadata;

            if (key != null)
            {
                attachmentMetadata.Key = key;
                attachmentMetadata.FileName = fileName;
            }

            await _attachmentStorageService.UploadShareAttachmentAsync(stream, cipher.Id, organizationId,
                attachmentMetadata);

            // Previous call may alter metadata
            var updatedAttachment = new CipherAttachment
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                AttachmentId = attachmentId,
                AttachmentData = JsonSerializer.Serialize(originalAttachmentMetadata)
            };

            await _cipherRepository.UpdateAttachmentAsync(updatedAttachment);
        }
        catch
        {
            await _attachmentStorageService.CleanupAsync(cipher.Id);
            throw;
        }
    }

    public async Task<bool> ValidateCipherAttachmentFile(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        var (valid, realSize) = await _attachmentStorageService.ValidateFileAsync(cipher, attachmentData, _fileSizeLeeway);

        if (!valid || realSize > MAX_FILE_SIZE)
        {
            // File reported differs in size from that promised. Must be a rogue client. Delete Send
            await DeleteAttachmentAsync(cipher, attachmentData);
            return false;
        }
        // Update Send data if necessary
        if (realSize != attachmentData.Size)
        {
            attachmentData.Size = realSize.Value;
        }
        attachmentData.Validated = true;

        var updatedAttachment = new CipherAttachment
        {
            Id = cipher.Id,
            UserId = cipher.UserId,
            OrganizationId = cipher.OrganizationId,
            AttachmentId = attachmentData.AttachmentId,
            AttachmentData = JsonSerializer.Serialize(attachmentData)
        };


        await _cipherRepository.UpdateAttachmentAsync(updatedAttachment);

        return valid;
    }

    public async Task<AttachmentResponseData> GetAttachmentDownloadDataAsync(Cipher cipher, string attachmentId)
    {
        var attachments = cipher?.GetAttachments() ?? new Dictionary<string, CipherAttachment.MetaData>();

        if (!attachments.ContainsKey(attachmentId))
        {
            throw new NotFoundException();
        }

        var data = attachments[attachmentId];
        var response = new AttachmentResponseData
        {
            Cipher = cipher,
            Data = data,
            Id = attachmentId,
            Url = await _attachmentStorageService.GetAttachmentDownloadUrlAsync(cipher, data),
        };

        return response;
    }

    public async Task DeleteAsync(Cipher cipher, Guid deletingUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !(await UserCanEditAsync(cipher, deletingUserId)))
        {
            throw new BadRequestException("You do not have permissions to delete this.");
        }

        await _cipherRepository.DeleteAsync(cipher);
        await _attachmentStorageService.DeleteAttachmentsForCipherAsync(cipher.Id);
        await _eventService.LogCipherEventAsync(cipher, EventType.Cipher_Deleted);

        // push
        await _pushService.PushSyncCipherDeleteAsync(cipher);
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId, Guid? organizationId = null, bool orgAdmin = false)
    {
        var cipherIdsSet = new HashSet<Guid>(cipherIds);
        var deletingCiphers = new List<Cipher>();

        if (orgAdmin && organizationId.HasValue)
        {
            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(organizationId.Value);
            deletingCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id)).ToList();
            await _cipherRepository.DeleteByIdsOrganizationIdAsync(deletingCiphers.Select(c => c.Id), organizationId.Value);
        }
        else
        {
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(deletingUserId);
            deletingCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id) && c.Edit).Select(x => (Cipher)x).ToList();

            await _cipherRepository.DeleteAsync(deletingCiphers.Select(c => c.Id), deletingUserId);
        }

        var events = deletingCiphers.Select(c =>
            new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_Deleted, null));
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        // push
        await _pushService.PushSyncCiphersAsync(deletingUserId);
    }

    public async Task<DeleteAttachmentResponseData> DeleteAttachmentAsync(Cipher cipher, string attachmentId, Guid deletingUserId,
        bool orgAdmin = false)
    {
        if (!orgAdmin && !(await UserCanEditAsync(cipher, deletingUserId)))
        {
            throw new BadRequestException("You do not have permissions to delete this.");
        }

        if (!cipher.ContainsAttachment(attachmentId))
        {
            throw new NotFoundException();
        }

        return await DeleteAttachmentAsync(cipher, cipher.GetAttachments()[attachmentId]);
    }

    public async Task PurgeAsync(Guid organizationId)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }
        await _cipherRepository.DeleteByOrganizationIdAsync(organizationId);
        await _eventService.LogOrganizationEventAsync(org, Bit.Core.Enums.EventType.Organization_PurgedVault);
    }

    public async Task MoveManyAsync(IEnumerable<Guid> cipherIds, Guid? destinationFolderId, Guid movingUserId)
    {
        if (destinationFolderId.HasValue)
        {
            var folder = await _folderRepository.GetByIdAsync(destinationFolderId.Value);
            if (folder == null || folder.UserId != movingUserId)
            {
                throw new BadRequestException("Invalid folder.");
            }
        }

        await _cipherRepository.MoveAsync(cipherIds, destinationFolderId, movingUserId);
        // push
        await _pushService.PushSyncCiphersAsync(movingUserId);
    }

    public async Task SaveFolderAsync(Folder folder)
    {
        if (folder.Id == default(Guid))
        {
            await _folderRepository.CreateAsync(folder);

            // push
            await _pushService.PushSyncFolderCreateAsync(folder);
        }
        else
        {
            folder.RevisionDate = DateTime.UtcNow;
            await _folderRepository.UpsertAsync(folder);

            // push
            await _pushService.PushSyncFolderUpdateAsync(folder);
        }
    }

    public async Task DeleteFolderAsync(Folder folder)
    {
        await _folderRepository.DeleteAsync(folder);

        // push
        await _pushService.PushSyncFolderDeleteAsync(folder);
    }

    public async Task ShareAsync(Cipher originalCipher, Cipher cipher, Guid organizationId,
        IEnumerable<Guid> collectionIds, Guid sharingUserId, DateTime? lastKnownRevisionDate)
    {
        var attachments = cipher.GetAttachments();
        var hasOldAttachments = attachments?.Values?.Any(a => a.Key == null) ?? false;
        var updatedCipher = false;
        var migratedAttachments = false;
        var originalAttachments = CoreHelpers.CloneObject(originalCipher.GetAttachments());

        try
        {
            await ValidateCipherCanBeShared(cipher, sharingUserId, organizationId, lastKnownRevisionDate);

            // Sproc will not save this UserId on the cipher. It is used limit scope of the collectionIds.
            cipher.UserId = sharingUserId;
            cipher.OrganizationId = organizationId;
            cipher.RevisionDate = DateTime.UtcNow;

            if (hasOldAttachments)
            {
                var attachmentsWithUpdatedMetadata = originalCipher.GetAttachments();
                var attachmentsToUpdateMetadata = CoreHelpers.CloneObject(attachments);
                foreach (var updatedMetadata in attachmentsWithUpdatedMetadata.Where(a => a.Value?.TempMetadata != null))
                {
                    if (attachmentsToUpdateMetadata.ContainsKey(updatedMetadata.Key))
                    {
                        attachmentsToUpdateMetadata[updatedMetadata.Key] = updatedMetadata.Value.TempMetadata;
                    }
                }
                cipher.SetAttachments(attachmentsToUpdateMetadata);
            }

            if (!await _cipherRepository.ReplaceAsync(cipher, collectionIds))
            {
                throw new BadRequestException("Unable to save.");
            }

            updatedCipher = true;
            await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_Shared);

            if (hasOldAttachments)
            {
                // migrate old attachments
                foreach (var attachment in attachments.Values.Where(a => a.TempMetadata != null).Select(a => a.TempMetadata))
                {
                    await _attachmentStorageService.StartShareAttachmentAsync(cipher.Id, organizationId,
                        attachment);
                    migratedAttachments = true;
                }

                // commit attachment migration
                await _attachmentStorageService.CleanupAsync(cipher.Id);
            }
        }
        catch
        {
            // roll everything back
            if (updatedCipher)
            {
                if (hasOldAttachments)
                {
                    foreach (var item in originalAttachments)
                    {
                        item.Value.TempMetadata = null;
                    }
                    originalCipher.SetAttachments(originalAttachments);
                }

                var currentCollectionsForCipher = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(sharingUserId, originalCipher.Id);
                var currentCollectionIdsForCipher = currentCollectionsForCipher.Select(c => c.CollectionId).ToList();
                currentCollectionIdsForCipher.RemoveAll(id => collectionIds.Contains(id));

                await _collectionCipherRepository.UpdateCollectionsAsync(originalCipher.Id, sharingUserId, currentCollectionIdsForCipher);
                await _cipherRepository.ReplaceAsync(originalCipher);
            }

            if (!hasOldAttachments || !migratedAttachments)
            {
                throw;
            }

            if (updatedCipher)
            {
                await _userRepository.UpdateStorageAsync(sharingUserId);
                await _organizationRepository.UpdateStorageAsync(organizationId);
            }

            foreach (var attachment in attachments.Where(a => a.Value.Key == null))
            {
                await _attachmentStorageService.RollbackShareAttachmentAsync(cipher.Id, organizationId,
                    attachment.Value, originalAttachments[attachment.Key].ContainerName);
            }

            await _attachmentStorageService.CleanupAsync(cipher.Id);
            throw;
        }

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
    }

    public async Task ShareManyAsync(IEnumerable<(Cipher cipher, DateTime? lastKnownRevisionDate)> cipherInfos,
        Guid organizationId, IEnumerable<Guid> collectionIds, Guid sharingUserId)
    {
        var cipherIds = new List<Guid>();
        foreach (var (cipher, lastKnownRevisionDate) in cipherInfos)
        {
            await ValidateCipherCanBeShared(cipher, sharingUserId, organizationId, lastKnownRevisionDate);

            cipher.UserId = null;
            cipher.OrganizationId = organizationId;
            cipher.RevisionDate = DateTime.UtcNow;
            cipherIds.Add(cipher.Id);
        }

        await _cipherRepository.UpdateCiphersAsync(sharingUserId, cipherInfos.Select(c => c.cipher));
        await _collectionCipherRepository.UpdateCollectionsForCiphersAsync(cipherIds, sharingUserId,
            organizationId, collectionIds);

        var events = cipherInfos.Select(c =>
            new Tuple<Cipher, EventType, DateTime?>(c.cipher, EventType.Cipher_Shared, null));
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        // push
        await _pushService.PushSyncCiphersAsync(sharingUserId);
    }

    public async Task SaveCollectionsAsync(Cipher cipher, IEnumerable<Guid> collectionIds, Guid savingUserId,
        bool orgAdmin)
    {
        if (cipher.Id == default(Guid))
        {
            throw new BadRequestException(nameof(cipher.Id));
        }

        if (!cipher.OrganizationId.HasValue)
        {
            throw new BadRequestException("Cipher must belong to an organization.");
        }

        cipher.RevisionDate = DateTime.UtcNow;

        // The sprocs will validate that all collections belong to this org/user and that they have
        // proper write permissions.
        if (orgAdmin)
        {
            await _collectionCipherRepository.UpdateCollectionsForAdminAsync(cipher.Id,
                cipher.OrganizationId.Value, collectionIds);
        }
        else
        {
            if (!(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }
            await _collectionCipherRepository.UpdateCollectionsAsync(cipher.Id, savingUserId, collectionIds);
        }

        await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_UpdatedCollections);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
    }

    public async Task SoftDeleteAsync(Cipher cipher, Guid deletingUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !(await UserCanEditAsync(cipher, deletingUserId)))
        {
            throw new BadRequestException("You do not have permissions to soft delete this.");
        }

        if (cipher.DeletedDate.HasValue)
        {
            // Already soft-deleted, we can safely ignore this
            return;
        }

        cipher.DeletedDate = cipher.RevisionDate = DateTime.UtcNow;

        if (cipher is CipherDetails details)
        {
            await _cipherRepository.UpsertAsync(details);
        }
        else
        {
            await _cipherRepository.UpsertAsync(cipher);
        }
        await _eventService.LogCipherEventAsync(cipher, EventType.Cipher_SoftDeleted);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, null);
    }

    public async Task SoftDeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId, Guid? organizationId, bool orgAdmin)
    {
        var cipherIdsSet = new HashSet<Guid>(cipherIds);
        var deletingCiphers = new List<Cipher>();

        if (orgAdmin && organizationId.HasValue)
        {
            var ciphers = await _cipherRepository.GetManyByOrganizationIdAsync(organizationId.Value);
            deletingCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id)).ToList();
            await _cipherRepository.SoftDeleteByIdsOrganizationIdAsync(deletingCiphers.Select(c => c.Id), organizationId.Value);
        }
        else
        {
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(deletingUserId);
            deletingCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id) && c.Edit).Select(x => (Cipher)x).ToList();

            await _cipherRepository.SoftDeleteAsync(deletingCiphers.Select(c => c.Id), deletingUserId);
        }

        var events = deletingCiphers.Select(c =>
            new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_SoftDeleted, null));
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        // push
        await _pushService.PushSyncCiphersAsync(deletingUserId);
    }

    public async Task RestoreAsync(Cipher cipher, Guid restoringUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !(await UserCanEditAsync(cipher, restoringUserId)))
        {
            throw new BadRequestException("You do not have permissions to delete this.");
        }

        if (!cipher.DeletedDate.HasValue)
        {
            // Already restored, we can safely ignore this
            return;
        }

        cipher.DeletedDate = null;
        cipher.RevisionDate = DateTime.UtcNow;

        if (cipher is CipherDetails details)
        {
            await _cipherRepository.UpsertAsync(details);
        }
        else
        {
            await _cipherRepository.UpsertAsync(cipher);
        }
        await _eventService.LogCipherEventAsync(cipher, EventType.Cipher_Restored);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, null);
    }

    public async Task<ICollection<CipherOrganizationDetails>> RestoreManyAsync(IEnumerable<Guid> cipherIds, Guid restoringUserId, Guid? organizationId = null, bool orgAdmin = false)
    {
        if (cipherIds == null || !cipherIds.Any())
        {
            return new List<CipherOrganizationDetails>();
        }

        var cipherIdsSet = new HashSet<Guid>(cipherIds);
        var restoringCiphers = new List<CipherOrganizationDetails>();
        DateTime? revisionDate;

        if (orgAdmin && organizationId.HasValue)
        {
            var ciphers = await _cipherRepository.GetManyOrganizationDetailsByOrganizationIdAsync(organizationId.Value);
            restoringCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id)).ToList();
            revisionDate = await _cipherRepository.RestoreByIdsOrganizationIdAsync(restoringCiphers.Select(c => c.Id), organizationId.Value);
        }
        else
        {
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(restoringUserId);
            restoringCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id) && c.Edit).Select(c => (CipherOrganizationDetails)c).ToList();

            revisionDate = await _cipherRepository.RestoreAsync(restoringCiphers.Select(c => c.Id), restoringUserId);
        }

        var events = restoringCiphers.Select(c =>
        {
            c.RevisionDate = revisionDate.Value;
            c.DeletedDate = null;
            return new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_Restored, null);
        });
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        // push
        await _pushService.PushSyncCiphersAsync(restoringUserId);

        return restoringCiphers;
    }

    private async Task<bool> UserCanEditAsync(Cipher cipher, Guid userId)
    {
        if (!cipher.OrganizationId.HasValue && cipher.UserId.HasValue && cipher.UserId.Value == userId)
        {
            return true;
        }

        return await _cipherRepository.GetCanEditByIdAsync(userId, cipher.Id);
    }

    private void ValidateCipherLastKnownRevisionDateAsync(Cipher cipher, DateTime? lastKnownRevisionDate)
    {
        if (cipher.Id == default || !lastKnownRevisionDate.HasValue)
        {
            return;
        }

        if ((cipher.RevisionDate - lastKnownRevisionDate.Value).Duration() > TimeSpan.FromSeconds(1))
        {
            throw new BadRequestException(
                "The cipher you are updating is out of date. Please save your work, sync your vault, and try again."
            );
        }
    }

    private async Task<DeleteAttachmentResponseData> DeleteAttachmentAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        if (attachmentData == null || string.IsNullOrWhiteSpace(attachmentData.AttachmentId))
        {
            return null;
        }

        await _cipherRepository.DeleteAttachmentAsync(cipher.Id, attachmentData.AttachmentId);
        cipher.DeleteAttachment(attachmentData.AttachmentId);
        await _attachmentStorageService.DeleteAttachmentAsync(cipher.Id, attachmentData);
        await _eventService.LogCipherEventAsync(cipher, Bit.Core.Enums.EventType.Cipher_AttachmentDeleted);

        // Update the revision date when an attachment is deleted
        cipher.RevisionDate = DateTime.UtcNow;
        await _cipherRepository.ReplaceAsync((CipherDetails)cipher);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, null);

        return new DeleteAttachmentResponseData(cipher);
    }

    private async Task ValidateCipherEditForAttachmentAsync(Cipher cipher, Guid savingUserId, bool orgAdmin,
        long requestLength)
    {
        if (!orgAdmin && !(await UserCanEditAsync(cipher, savingUserId)))
        {
            throw new BadRequestException("You do not have permissions to edit this.");
        }

        if (requestLength < 1)
        {
            throw new BadRequestException("No data to attach.");
        }

        var storageBytesRemaining = await StorageBytesRemainingForCipherAsync(cipher);

        if (storageBytesRemaining < requestLength)
        {
            throw new BadRequestException("Not enough storage available.");
        }
    }

    private async Task<long> StorageBytesRemainingForCipherAsync(Cipher cipher)
    {
        var storageBytesRemaining = 0L;
        if (cipher.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(cipher.UserId.Value);
            if (!(await _userService.CanAccessPremium(user)))
            {
                throw new BadRequestException("You must have premium status to use attachments.");
            }

            if (user.Premium)
            {
                storageBytesRemaining = user.StorageBytesRemaining();
            }
            else
            {
                // Users that get access to file storage/premium from their organization get the default
                // 1 GB max storage.
                storageBytesRemaining = user.StorageBytesRemaining(
                    _globalSettings.SelfHosted ? (short)10240 : (short)1);
            }
        }
        else if (cipher.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value);
            if (!org.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("This organization cannot use attachments.");
            }

            storageBytesRemaining = org.StorageBytesRemaining();
        }

        return storageBytesRemaining;
    }

    private async Task ValidateCipherCanBeShared(
        Cipher cipher,
        Guid sharingUserId,
        Guid organizationId,
        DateTime? lastKnownRevisionDate)
    {
        if (cipher.Id == default(Guid))
        {
            throw new BadRequestException("Cipher must already exist.");
        }

        if (cipher.OrganizationId.HasValue)
        {
            throw new BadRequestException("One or more ciphers already belong to an organization.");
        }

        if (!cipher.UserId.HasValue || cipher.UserId.Value != sharingUserId)
        {
            throw new BadRequestException("One or more ciphers do not belong to you.");
        }

        var attachments = cipher.GetAttachments();
        var hasAttachments = attachments?.Any() ?? false;
        var org = await _organizationRepository.GetByIdAsync(organizationId);

        if (org == null)
        {
            throw new BadRequestException("Could not find organization.");
        }

        if (hasAttachments && !org.MaxStorageGb.HasValue)
        {
            throw new BadRequestException("This organization cannot use attachments.");
        }

        var storageAdjustment = attachments?.Sum(a => a.Value.Size) ?? 0;
        if (org.StorageBytesRemaining() < storageAdjustment)
        {
            throw new BadRequestException("Not enough storage available for this organization.");
        }

        ValidateCipherLastKnownRevisionDateAsync(cipher, lastKnownRevisionDate);
    }

    private async Task ValidateViewPasswordUserAsync(Cipher cipher)
    {
        if (cipher.Type != CipherType.Login || cipher.Data == null || !cipher.OrganizationId.HasValue)
        {
            return;
        }
        var existingCipher = await _cipherRepository.GetByIdAsync(cipher.Id);
        if (existingCipher == null) return;

        var cipherPermissions = await _getCipherPermissionsForUserQuery.GetByOrganization(cipher.OrganizationId.Value);
        // Check if user is a "hidden password" user
        if (!cipherPermissions.TryGetValue(cipher.Id, out var permission) || !(permission.ViewPassword && permission.Edit))
        {
            // "hidden password" users may not add cipher key encryption
            if (existingCipher.Key == null && cipher.Key != null)
            {
                throw new BadRequestException("You do not have permission to add cipher key encryption.");
            }
            // "hidden password" users may not change passwords, TOTP codes, or passkeys, so we need to set them back to the original values
            var existingCipherData = JsonSerializer.Deserialize<CipherLoginData>(existingCipher.Data);
            var newCipherData = JsonSerializer.Deserialize<CipherLoginData>(cipher.Data);
            newCipherData.Fido2Credentials = existingCipherData.Fido2Credentials;
            newCipherData.Totp = existingCipherData.Totp;
            newCipherData.Password = existingCipherData.Password;
            cipher.Data = JsonSerializer.Serialize(newCipherData);
        }
    }
}
