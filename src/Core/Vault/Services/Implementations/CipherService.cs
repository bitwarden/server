// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Authorization.Permissions;
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
    private readonly ISecurityTaskRepository _securityTaskRepository;
    private readonly IPushNotificationService _pushService;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly IEventService _eventService;
    private readonly IUserService _userService;
    private readonly IPolicyService _policyService;
    private readonly GlobalSettings _globalSettings;
    private const long _fileSizeLeeway = 1024L * 1024L; // 1MB
    private readonly IGetCipherPermissionsForUserQuery _getCipherPermissionsForUserQuery;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;

    public CipherService(
        ICipherRepository cipherRepository,
        IFolderRepository folderRepository,
        ICollectionRepository collectionRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ISecurityTaskRepository securityTaskRepository,
        IPushNotificationService pushService,
        IAttachmentStorageService attachmentStorageService,
        IEventService eventService,
        IUserService userService,
        IPolicyService policyService,
        GlobalSettings globalSettings,
        IGetCipherPermissionsForUserQuery getCipherPermissionsForUserQuery,
        IPolicyRequirementQuery policyRequirementQuery,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService)
    {
        _cipherRepository = cipherRepository;
        _folderRepository = folderRepository;
        _collectionRepository = collectionRepository;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _securityTaskRepository = securityTaskRepository;
        _pushService = pushService;
        _attachmentStorageService = attachmentStorageService;
        _eventService = eventService;
        _userService = userService;
        _policyService = policyService;
        _globalSettings = globalSettings;
        _getCipherPermissionsForUserQuery = getCipherPermissionsForUserQuery;
        _policyRequirementQuery = policyRequirementQuery;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
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
            ValidateCipherLastKnownRevisionDate(cipher, lastKnownRevisionDate);
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
                var organizationDataOwnershipEnabled = _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements)
                    ? (await _policyRequirementQuery.GetAsync<OrganizationDataOwnershipPolicyRequirement>(savingUserId)).State == OrganizationDataOwnershipState.Enabled
                    : await _policyService.AnyPoliciesApplicableToUserAsync(savingUserId, PolicyType.OrganizationDataOwnership);

                if (organizationDataOwnershipEnabled)
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
            ValidateCipherLastKnownRevisionDate(cipher, lastKnownRevisionDate);
            cipher.RevisionDate = DateTime.UtcNow;
            await ValidateChangeInCollectionsAsync(cipher, collectionIds, savingUserId);
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
        string key, string fileName, long fileSize, bool adminRequest, Guid savingUserId, DateTime? lastKnownRevisionDate = null)
    {
        ValidateCipherLastKnownRevisionDate(cipher, lastKnownRevisionDate);
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
        long requestLength, Guid savingUserId, bool orgAdmin = false, DateTime? lastKnownRevisionDate = null)
    {
        ValidateCipherLastKnownRevisionDate(cipher, lastKnownRevisionDate);
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
            if (!attachments.TryGetValue(attachmentId, out var originalAttachmentMetadata))
            {
                throw new BadRequestException($"Cipher does not own specified attachment");
            }

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
            await DeleteAttachmentAsync(cipher, attachmentData, false);
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

        if (!attachments.TryGetValue(attachmentId, out var data))
        {
            throw new NotFoundException();
        }

        var response = new AttachmentResponseData
        {
            Cipher = cipher,
            Data = data,
            Id = attachmentId,
            Url = await _attachmentStorageService.GetAttachmentDownloadUrlAsync(cipher, data),
        };

        return response;
    }

    public async Task DeleteAsync(CipherDetails cipherDetails, Guid deletingUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !await UserCanDeleteAsync(cipherDetails, deletingUserId))
        {
            throw new BadRequestException("You do not have permissions to delete this.");
        }

        await _cipherRepository.DeleteAsync(cipherDetails);
        await _attachmentStorageService.DeleteAttachmentsForCipherAsync(cipherDetails.Id);
        await _eventService.LogCipherEventAsync(cipherDetails, EventType.Cipher_Deleted);

        // push
        await _pushService.PushSyncCipherDeleteAsync(cipherDetails);
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
            var filteredCiphers = await FilterCiphersByDeletePermission(ciphers, cipherIdsSet, deletingUserId);
            deletingCiphers = filteredCiphers.Select(c => (Cipher)c).ToList();
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

        return await DeleteAttachmentAsync(cipher, cipher.GetAttachments()[attachmentId], orgAdmin);
    }

    public async Task PurgeAsync(Guid organizationId)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }
        await _cipherRepository.DeleteByOrganizationIdAsync(organizationId);
        await _eventService.LogOrganizationEventAsync(org, EventType.Organization_PurgedVault);
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
            await ValidateChangeInCollectionsAsync(cipher, collectionIds, sharingUserId);

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

    public async Task<IEnumerable<CipherDetails>> ShareManyAsync(IEnumerable<(CipherDetails cipher, DateTime? lastKnownRevisionDate)> cipherInfos,
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
        return cipherInfos.Select(c => c.cipher);
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
        await ValidateChangeInCollectionsAsync(cipher, collectionIds, savingUserId);

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

        await _eventService.LogCipherEventAsync(cipher, EventType.Cipher_UpdatedCollections);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
    }

    public async Task SoftDeleteAsync(CipherDetails cipherDetails, Guid deletingUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !await UserCanDeleteAsync(cipherDetails, deletingUserId))
        {
            throw new BadRequestException("You do not have permissions to soft delete this.");
        }

        if (cipherDetails.DeletedDate.HasValue)
        {
            // Already soft-deleted, we can safely ignore this
            return;
        }

        cipherDetails.DeletedDate = cipherDetails.RevisionDate = DateTime.UtcNow;

        if (cipherDetails.ArchivedDate.HasValue)
        {
            // If the cipher was archived, clear the archived date when soft deleting
            // If a user were to restore an archived cipher, it should go back to the vault not the archive vault
            cipherDetails.ArchivedDate = null;
        }

        await _securityTaskRepository.MarkAsCompleteByCipherIds([cipherDetails.Id]);
        await _cipherRepository.UpsertAsync(cipherDetails);
        await _eventService.LogCipherEventAsync(cipherDetails, EventType.Cipher_SoftDeleted);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipherDetails, null);
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
            var filteredCiphers = await FilterCiphersByDeletePermission(ciphers, cipherIdsSet, deletingUserId);
            deletingCiphers = filteredCiphers.Select(c => (Cipher)c).ToList();
            await _cipherRepository.SoftDeleteAsync(deletingCiphers.Select(c => c.Id), deletingUserId);
        }

        await _securityTaskRepository.MarkAsCompleteByCipherIds(deletingCiphers.Select(c => c.Id));

        var events = deletingCiphers.Select(c =>
            new Tuple<Cipher, EventType, DateTime?>(c, EventType.Cipher_SoftDeleted, null));
        foreach (var eventsBatch in events.Chunk(100))
        {
            await _eventService.LogCipherEventsAsync(eventsBatch);
        }

        // push
        await _pushService.PushSyncCiphersAsync(deletingUserId);
    }

    public async Task RestoreAsync(CipherDetails cipherDetails, Guid restoringUserId, bool orgAdmin = false)
    {
        if (!orgAdmin && !await UserCanRestoreAsync(cipherDetails, restoringUserId))
        {
            throw new BadRequestException("You do not have permissions to delete this.");
        }

        if (!cipherDetails.DeletedDate.HasValue)
        {
            // Already restored, we can safely ignore this
            return;
        }

        cipherDetails.DeletedDate = null;
        cipherDetails.RevisionDate = DateTime.UtcNow;

        await _cipherRepository.UpsertAsync(cipherDetails);
        await _eventService.LogCipherEventAsync(cipherDetails, EventType.Cipher_Restored);

        // push
        await _pushService.PushSyncCipherUpdateAsync(cipherDetails, null);
    }

    public async Task<ICollection<CipherOrganizationDetails>> RestoreManyAsync(IEnumerable<Guid> cipherIds, Guid restoringUserId, Guid? organizationId = null, bool orgAdmin = false)
    {
        if (cipherIds == null || !cipherIds.Any())
        {
            return new List<CipherOrganizationDetails>();
        }

        var cipherIdsSet = new HashSet<Guid>(cipherIds);
        List<CipherOrganizationDetails> restoringCiphers;
        DateTime? revisionDate; // TODO: Make this not nullable

        if (orgAdmin && organizationId.HasValue)
        {
            var ciphers = await _cipherRepository.GetManyOrganizationDetailsByOrganizationIdAsync(organizationId.Value);
            restoringCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id)).ToList();
            revisionDate = await _cipherRepository.RestoreByIdsOrganizationIdAsync(restoringCiphers.Select(c => c.Id), organizationId.Value);
        }
        else
        {
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(restoringUserId);
            var filteredCiphers = await FilterCiphersByDeletePermission(ciphers, cipherIdsSet, restoringUserId);
            restoringCiphers = filteredCiphers.Select(c => (CipherOrganizationDetails)c).ToList();
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

    public async Task ValidateBulkCollectionAssignmentAsync(IEnumerable<Guid> collectionIds, IEnumerable<Guid> cipherIds, Guid userId)
    {
        foreach (var cipherId in cipherIds)
        {
            var cipher = await _cipherRepository.GetByIdAsync(cipherId);
            await ValidateChangeInCollectionsAsync(cipher, collectionIds, userId);
        }
    }

    private async Task<bool> UserCanEditAsync(Cipher cipher, Guid userId)
    {
        if (!cipher.OrganizationId.HasValue && cipher.UserId.HasValue && cipher.UserId.Value == userId)
        {
            return true;
        }

        return await _cipherRepository.GetCanEditByIdAsync(userId, cipher.Id);
    }

    private async Task<bool> UserCanDeleteAsync(CipherDetails cipher, Guid userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        var organizationAbility = cipher.OrganizationId.HasValue ?
            await _applicationCacheService.GetOrganizationAbilityAsync(cipher.OrganizationId.Value) : null;

        return NormalCipherPermissions.CanDelete(user, cipher, organizationAbility);
    }

    private async Task<bool> UserCanRestoreAsync(CipherDetails cipher, Guid userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        var organizationAbility = cipher.OrganizationId.HasValue ?
            await _applicationCacheService.GetOrganizationAbilityAsync(cipher.OrganizationId.Value) : null;

        return NormalCipherPermissions.CanRestore(user, cipher, organizationAbility);
    }

    private void ValidateCipherLastKnownRevisionDate(Cipher cipher, DateTime? lastKnownRevisionDate)
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

    private async Task<DeleteAttachmentResponseData> DeleteAttachmentAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, bool orgAdmin)
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
        if (orgAdmin)
        {
            await _cipherRepository.ReplaceAsync(cipher);
        }
        else
        {
            await _cipherRepository.ReplaceAsync((CipherDetails)cipher);
        }

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
                    _globalSettings.SelfHosted ? Constants.SelfHostedMaxStorageGb : (short)1);
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

        if (cipher.ArchivedDate.HasValue)
        {
            throw new BadRequestException("Cipher cannot be shared with organization because it is archived.");
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

        ValidateCipherLastKnownRevisionDate(cipher, lastKnownRevisionDate);
    }

    private async Task ValidateViewPasswordUserAsync(Cipher cipher)
    {
        if (cipher.Data == null || !cipher.OrganizationId.HasValue)
        {
            return;
        }
        var existingCipher = await _cipherRepository.GetByIdAsync(cipher.Id);
        if (existingCipher == null) return;

        var cipherPermissions = await _getCipherPermissionsForUserQuery.GetByOrganization(cipher.OrganizationId.Value);
        // Check if user is a "hidden password" user
        if (!cipherPermissions.TryGetValue(cipher.Id, out var permission) || !(permission.ViewPassword && permission.Edit))
        {
            var existingCipherData = DeserializeCipherData(existingCipher);
            var newCipherData = DeserializeCipherData(cipher);

            // "hidden password" users may not add cipher key encryption
            if (existingCipher.Key == null && cipher.Key != null)
            {
                throw new BadRequestException("You do not have permission to add cipher key encryption.");
            }
            // Keep only non-hidden fileds from the new cipher
            var nonHiddenFields = newCipherData.Fields?.Where(f => f.Type != FieldType.Hidden) ?? [];
            // Get hidden fields from the existing cipher
            var hiddenFields = existingCipherData.Fields?.Where(f => f.Type == FieldType.Hidden) ?? [];
            // Replace the hidden fields in new cipher data with the existing ones
            newCipherData.Fields = nonHiddenFields.Concat(hiddenFields);
            cipher.Data = SerializeCipherData(newCipherData);
            if (existingCipherData is CipherLoginData existingLoginData && newCipherData is CipherLoginData newLoginCipherData)
            {
                // "hidden password" users may not change passwords, TOTP codes, or passkeys, so we need to set them back to the original values
                newLoginCipherData.Fido2Credentials = existingLoginData.Fido2Credentials;
                newLoginCipherData.Totp = existingLoginData.Totp;
                newLoginCipherData.Password = existingLoginData.Password;
                cipher.Data = SerializeCipherData(newLoginCipherData);
            }
        }
    }

    // Validates that a cipher is not being added to a default collection when it is only currently only in shared collections
    private async Task ValidateChangeInCollectionsAsync(Cipher updatedCipher, IEnumerable<Guid> newCollectionIds, Guid userId)
    {

        if (updatedCipher.Id == Guid.Empty || !updatedCipher.OrganizationId.HasValue)
        {
            return;
        }

        var currentCollectionsForCipher = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, updatedCipher.Id);

        if (!currentCollectionsForCipher.Any())
        {
            // When a cipher is not currently in any collections it can be assigned to any type of collection
            return;
        }

        var currentCollections = await _collectionRepository.GetManyByManyIdsAsync(currentCollectionsForCipher.Select(c => c.CollectionId));

        var currentCollectionsContainDefault = currentCollections.Any(c => c.Type == CollectionType.DefaultUserCollection);

        // When the current cipher already contains the default collection, no check is needed for if they added or removed
        // a default collection, because it is already there.
        if (currentCollectionsContainDefault)
        {
            return;
        }

        var newCollections = await _collectionRepository.GetManyByManyIdsAsync(newCollectionIds);
        var newCollectionsContainDefault = newCollections.Any(c => c.Type == CollectionType.DefaultUserCollection);

        if (newCollectionsContainDefault)
        {
            // User is trying to add the default collection when the cipher is only in shared collections
            throw new BadRequestException("The cipher(s) cannot be assigned to a default collection when only assigned to non-default collections.");
        }
    }

    private string SerializeCipherData(CipherData data)
    {
        return data switch
        {
            CipherLoginData loginData => JsonSerializer.Serialize(loginData),
            CipherIdentityData identityData => JsonSerializer.Serialize(identityData),
            CipherCardData cardData => JsonSerializer.Serialize(cardData),
            CipherSecureNoteData noteData => JsonSerializer.Serialize(noteData),
            CipherSSHKeyData sshKeyData => JsonSerializer.Serialize(sshKeyData),
            _ => throw new ArgumentException("Unsupported cipher data type.", nameof(data))
        };
    }

    private CipherData DeserializeCipherData(Cipher cipher)
    {
        return cipher.Type switch
        {
            CipherType.Login => JsonSerializer.Deserialize<CipherLoginData>(cipher.Data),
            CipherType.Identity => JsonSerializer.Deserialize<CipherIdentityData>(cipher.Data),
            CipherType.Card => JsonSerializer.Deserialize<CipherCardData>(cipher.Data),
            CipherType.SecureNote => JsonSerializer.Deserialize<CipherSecureNoteData>(cipher.Data),
            CipherType.SSHKey => JsonSerializer.Deserialize<CipherSSHKeyData>(cipher.Data),
            _ => throw new ArgumentException("Unsupported cipher type.", nameof(cipher))
        };
    }

    // This method is used to filter ciphers based on the user's permissions to delete them.
    private async Task<List<T>> FilterCiphersByDeletePermission<T>(
        IEnumerable<T> ciphers,
        HashSet<Guid> cipherIdsSet,
        Guid userId) where T : CipherDetails
    {
        var user = await _userService.GetUserByIdAsync(userId);
        var organizationAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();

        var filteredCiphers = ciphers
            .Where(c => cipherIdsSet.Contains(c.Id))
            .GroupBy(c => c.OrganizationId)
            .SelectMany(group =>
            {
                var organizationAbility = group.Key.HasValue &&
                    organizationAbilities.TryGetValue(group.Key.Value, out var ability) ?
                    ability : null;

                return group.Where(c => NormalCipherPermissions.CanDelete(user, c, organizationAbility));
            })
            .ToList();

        return filteredCiphers;
    }
}
