using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Core.Models.Data;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Newtonsoft.Json;
using System.IO;

namespace Bit.Core.Services
{
    public class CipherService : ICipherService
    {
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
        private readonly GlobalSettings _globalSettings;

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
            GlobalSettings globalSettings)
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
            _globalSettings = globalSettings;
        }

        public async Task SaveAsync(Cipher cipher, Guid savingUserId, IEnumerable<Guid> collectionIds = null,
            bool skipPermissionCheck = false, bool limitCollectionScope = true)
        {
            if(!skipPermissionCheck && !(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }

            if(cipher.Id == default(Guid))
            {
                if(cipher.OrganizationId.HasValue && collectionIds != null)
                {
                    if(limitCollectionScope)
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
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Created);

                // push
                await _pushService.PushSyncCipherCreateAsync(cipher, null);
            }
            else
            {
                if(collectionIds != null)
                {
                    throw new ArgumentException("Cannot create cipher with collection ids at the same time.");
                }
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Updated);

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher, null);
            }
        }

        public async Task SaveDetailsAsync(CipherDetails cipher, Guid savingUserId,
            IEnumerable<Guid> collectionIds = null, bool skipPermissionCheck = false)
        {
            if(!skipPermissionCheck && !(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }

            cipher.UserId = savingUserId;
            if(cipher.Id == default(Guid))
            {
                if(cipher.OrganizationId.HasValue && collectionIds != null)
                {
                    await _cipherRepository.CreateAsync(cipher, collectionIds);
                }
                else
                {
                    await _cipherRepository.CreateAsync(cipher);
                }
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Created);

                if(cipher.OrganizationId.HasValue)
                {
                    var org = await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value);
                    cipher.OrganizationUseTotp = org.UseTotp;
                }

                // push
                await _pushService.PushSyncCipherCreateAsync(cipher, null);
            }
            else
            {
                if(collectionIds != null)
                {
                    throw new ArgumentException("Cannot create cipher with collection ids at the same time.");
                }
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Updated);

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher, null);
            }
        }

        public async Task CreateAttachmentAsync(Cipher cipher, Stream stream, string fileName, string key,
            long requestLength, Guid savingUserId, bool orgAdmin = false)
        {
            if(!orgAdmin && !(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }

            if(requestLength < 1)
            {
                throw new BadRequestException("No data to attach.");
            }

            var storageBytesRemaining = 0L;
            if(cipher.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(cipher.UserId.Value);
                if(!(await _userService.CanAccessPremium(user)))
                {
                    throw new BadRequestException("You must have premium status to use attachments.");
                }

                if(user.Premium)
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
            else if(cipher.OrganizationId.HasValue)
            {
                var org = await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value);
                if(!org.MaxStorageGb.HasValue)
                {
                    throw new BadRequestException("This organization cannot use attachments.");
                }

                storageBytesRemaining = org.StorageBytesRemaining();
            }

            if(storageBytesRemaining < requestLength)
            {
                throw new BadRequestException("Not enough storage available.");
            }

            var attachmentId = Utilities.CoreHelpers.SecureRandomString(32, upper: false, special: false);
            await _attachmentStorageService.UploadNewAttachmentAsync(stream, cipher, attachmentId);

            try
            {
                var data = new CipherAttachment.MetaData
                {
                    FileName = fileName,
                    Key = key,
                    Size = stream.Length
                };

                var attachment = new CipherAttachment
                {
                    Id = cipher.Id,
                    UserId = cipher.UserId,
                    OrganizationId = cipher.OrganizationId,
                    AttachmentId = attachmentId,
                    AttachmentData = JsonConvert.SerializeObject(data)
                };

                await _cipherRepository.UpdateAttachmentAsync(attachment);
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_AttachmentCreated);
                cipher.AddAttachment(attachmentId, data);
            }
            catch
            {
                // Clean up since this is not transactional
                await _attachmentStorageService.DeleteAttachmentAsync(cipher.Id, attachmentId);
                throw;
            }

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher, null);
        }

        public async Task CreateAttachmentShareAsync(Cipher cipher, Stream stream, long requestLength,
            string attachmentId, Guid organizationId)
        {
            try
            {
                if(requestLength < 1)
                {
                    throw new BadRequestException("No data to attach.");
                }

                if(cipher.Id == default(Guid))
                {
                    throw new BadRequestException(nameof(cipher.Id));
                }

                if(cipher.OrganizationId.HasValue)
                {
                    throw new BadRequestException("Cipher belongs to an organization already.");
                }

                var org = await _organizationRepository.GetByIdAsync(organizationId);
                if(org == null || !org.MaxStorageGb.HasValue)
                {
                    throw new BadRequestException("This organization cannot use attachments.");
                }

                var storageBytesRemaining = org.StorageBytesRemaining();
                if(storageBytesRemaining < requestLength)
                {
                    throw new BadRequestException("Not enough storage available for this organization.");
                }

                await _attachmentStorageService.UploadShareAttachmentAsync(stream, cipher.Id, organizationId,
                    attachmentId);
            }
            catch
            {
                await _attachmentStorageService.CleanupAsync(cipher.Id);
                throw;
            }
        }

        public async Task DeleteAsync(Cipher cipher, Guid deletingUserId, bool orgAdmin = false)
        {
            if(!orgAdmin && !(await UserCanEditAsync(cipher, deletingUserId)))
            {
                throw new BadRequestException("You do not have permissions to delete this.");
            }

            await _cipherRepository.DeleteAsync(cipher);
            await _attachmentStorageService.DeleteAttachmentsForCipherAsync(cipher.Id);
            await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Deleted);

            // push
            await _pushService.PushSyncCipherDeleteAsync(cipher);
        }

        public async Task DeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId)
        {
            var cipherIdsSet = new HashSet<Guid>(cipherIds);
            var ciphers = await _cipherRepository.GetManyByUserIdAsync(deletingUserId);
            var deletingCiphers = ciphers.Where(c => cipherIdsSet.Contains(c.Id) && c.Edit);

            await _cipherRepository.DeleteAsync(cipherIds, deletingUserId);

            // TODO: move this to a single event?
            foreach(var cipher in deletingCiphers)
            {
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Deleted);
            }

            // push
            await _pushService.PushSyncCiphersAsync(deletingUserId);
        }

        public async Task DeleteAttachmentAsync(Cipher cipher, string attachmentId, Guid deletingUserId,
            bool orgAdmin = false)
        {
            if(!orgAdmin && !(await UserCanEditAsync(cipher, deletingUserId)))
            {
                throw new BadRequestException("You do not have permissions to delete this.");
            }

            if(!cipher.ContainsAttachment(attachmentId))
            {
                throw new NotFoundException();
            }

            await _cipherRepository.DeleteAttachmentAsync(cipher.Id, attachmentId);
            cipher.DeleteAttachment(attachmentId);
            await _attachmentStorageService.DeleteAttachmentAsync(cipher.Id, attachmentId);
            await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_AttachmentDeleted);

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher, null);
        }

        public async Task PurgeAsync(Guid organizationId)
        {
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            if(org == null)
            {
                throw new NotFoundException();
            }
            await _cipherRepository.DeleteByOrganizationIdAsync(organizationId);
            await _eventService.LogOrganizationEventAsync(org, Enums.EventType.Organization_PurgedVault);
        }

        public async Task MoveManyAsync(IEnumerable<Guid> cipherIds, Guid? destinationFolderId, Guid movingUserId)
        {
            if(destinationFolderId.HasValue)
            {
                var folder = await _folderRepository.GetByIdAsync(destinationFolderId.Value);
                if(folder == null || folder.UserId != movingUserId)
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
            if(folder.Id == default(Guid))
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
            IEnumerable<Guid> collectionIds, Guid sharingUserId)
        {
            var attachments = cipher.GetAttachments();
            var hasAttachments = attachments?.Any() ?? false;
            var hasOldAttachments = attachments?.Any(a => a.Key == null) ?? false;
            var updatedCipher = false;
            var migratedAttachments = false;

            try
            {
                if(cipher.Id == default(Guid))
                {
                    throw new BadRequestException(nameof(cipher.Id));
                }

                if(cipher.OrganizationId.HasValue)
                {
                    throw new BadRequestException("Already belongs to an organization.");
                }

                if(!cipher.UserId.HasValue || cipher.UserId.Value != sharingUserId)
                {
                    throw new NotFoundException();
                }

                var org = await _organizationRepository.GetByIdAsync(organizationId);
                if(hasAttachments && !org.MaxStorageGb.HasValue)
                {
                    throw new BadRequestException("This organization cannot use attachments.");
                }

                var storageAdjustment = attachments?.Sum(a => a.Value.Size) ?? 0;
                if(org.StorageBytesRemaining() < storageAdjustment)
                {
                    throw new BadRequestException("Not enough storage available for this organization.");
                }

                // Sproc will not save this UserId on the cipher. It is used limit scope of the collectionIds.
                cipher.UserId = sharingUserId;
                cipher.OrganizationId = organizationId;
                cipher.RevisionDate = DateTime.UtcNow;
                if(!await _cipherRepository.ReplaceAsync(cipher, collectionIds))
                {
                    throw new BadRequestException("Unable to save.");
                }

                updatedCipher = true;
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Shared);

                if(hasOldAttachments)
                {
                    // migrate old attachments
                    foreach(var attachment in attachments.Where(a => a.Key == null))
                    {
                        await _attachmentStorageService.StartShareAttachmentAsync(cipher.Id, organizationId,
                            attachment.Key);
                        migratedAttachments = true;
                    }

                    // commit attachment migration
                    await _attachmentStorageService.CleanupAsync(cipher.Id);
                }

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
            }
            catch
            {
                // roll everything back
                if(updatedCipher)
                {
                    await _cipherRepository.ReplaceAsync(originalCipher);
                }

                if(!hasOldAttachments || !migratedAttachments)
                {
                    throw;
                }

                if(updatedCipher)
                {
                    await _userRepository.UpdateStorageAsync(sharingUserId);
                    await _organizationRepository.UpdateStorageAsync(organizationId);
                }

                foreach(var attachment in attachments.Where(a => a.Key == null))
                {
                    await _attachmentStorageService.RollbackShareAttachmentAsync(cipher.Id, organizationId,
                        attachment.Key);
                }

                await _attachmentStorageService.CleanupAsync(cipher.Id);
                throw;
            }
        }

        public async Task ShareManyAsync(IEnumerable<Cipher> ciphers, Guid organizationId,
            IEnumerable<Guid> collectionIds, Guid sharingUserId)
        {
            var cipherIds = new List<Guid>();
            foreach(var cipher in ciphers)
            {
                if(cipher.Id == default(Guid))
                {
                    throw new BadRequestException("All ciphers must already exist.");
                }

                if(cipher.OrganizationId.HasValue)
                {
                    throw new BadRequestException("One or more ciphers already belong to an organization.");
                }

                if(!cipher.UserId.HasValue || cipher.UserId.Value != sharingUserId)
                {
                    throw new BadRequestException("One or more ciphers do not belong to you.");
                }

                cipher.UserId = null;
                cipher.OrganizationId = organizationId;
                cipher.RevisionDate = DateTime.UtcNow;
                cipherIds.Add(cipher.Id);
            }

            await _cipherRepository.UpdateCiphersAsync(sharingUserId, ciphers);
            await _collectionCipherRepository.UpdateCollectionsForCiphersAsync(cipherIds, sharingUserId,
                organizationId, collectionIds);

            // TODO: move this to a single event?
            foreach(var cipher in ciphers)
            {
                await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_Shared);
            }

            // push
            await _pushService.PushSyncCiphersAsync(sharingUserId);
        }

        public async Task SaveCollectionsAsync(Cipher cipher, IEnumerable<Guid> collectionIds, Guid savingUserId,
            bool orgAdmin)
        {
            if(cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if(!cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Cipher must belong to an organization.");
            }

            cipher.RevisionDate = DateTime.UtcNow;

            // The sprocs will validate that all collections belong to this org/user and that they have 
            // proper write permissions.
            if(orgAdmin)
            {
                await _collectionCipherRepository.UpdateCollectionsForAdminAsync(cipher.Id,
                    cipher.OrganizationId.Value, collectionIds);
            }
            else
            {
                if(!(await UserCanEditAsync(cipher, savingUserId)))
                {
                    throw new BadRequestException("You do not have permissions to edit this.");
                }
                await _collectionCipherRepository.UpdateCollectionsAsync(cipher.Id, savingUserId, collectionIds);
            }

            await _eventService.LogCipherEventAsync(cipher, Enums.EventType.Cipher_UpdatedCollections);

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher, collectionIds);
        }

        public async Task ImportCiphersAsync(
            List<Folder> folders,
            List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships)
        {
            foreach(var cipher in ciphers)
            {
                cipher.SetNewId();

                if(cipher.UserId.HasValue && cipher.Favorite)
                {
                    cipher.Favorites = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":\"true\"}}";
                }
            }

            // Init. ids for folders
            foreach(var folder in folders)
            {
                folder.SetNewId();
            }

            // Create the folder associations based on the newly created folder ids
            foreach(var relationship in folderRelationships)
            {
                var cipher = ciphers.ElementAtOrDefault(relationship.Key);
                var folder = folders.ElementAtOrDefault(relationship.Value);

                if(cipher == null || folder == null)
                {
                    continue;
                }

                cipher.Folders = $"{{\"{cipher.UserId.ToString().ToUpperInvariant()}\":" +
                    $"\"{folder.Id.ToString().ToUpperInvariant()}\"}}";
            }

            // Create it all
            await _cipherRepository.CreateAsync(ciphers, folders);

            // push
            var userId = folders.FirstOrDefault()?.UserId ?? ciphers.FirstOrDefault()?.UserId;
            if(userId.HasValue)
            {
                await _pushService.PushSyncVaultAsync(userId.Value);
            }
        }

        public async Task ImportCiphersAsync(
            List<Collection> collections,
            List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> collectionRelationships,
            Guid importingUserId)
        {
            if(collections.Count > 0)
            {
                var org = await _organizationRepository.GetByIdAsync(collections[0].OrganizationId);
                if(org != null && org.MaxCollections.HasValue)
                {
                    var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(org.Id);
                    if(org.MaxCollections.Value < (collectionCount + collections.Count))
                    {
                        throw new BadRequestException("This organization can only have a maximum of " +
                            $"{org.MaxCollections.Value} collections.");
                    }
                }
            }

            // Init. ids for ciphers
            foreach(var cipher in ciphers)
            {
                cipher.SetNewId();
            }

            // Init. ids for collections
            foreach(var collection in collections)
            {
                collection.SetNewId();
            }

            // Create associations based on the newly assigned ids
            var collectionCiphers = new List<CollectionCipher>();
            foreach(var relationship in collectionRelationships)
            {
                var cipher = ciphers.ElementAtOrDefault(relationship.Key);
                var collection = collections.ElementAtOrDefault(relationship.Value);

                if(cipher == null || collection == null)
                {
                    continue;
                }

                collectionCiphers.Add(new CollectionCipher
                {
                    CipherId = cipher.Id,
                    CollectionId = collection.Id
                });
            }

            // Create it all
            await _cipherRepository.CreateAsync(ciphers, collections, collectionCiphers);

            // push
            await _pushService.PushSyncVaultAsync(importingUserId);
        }

        private async Task<bool> UserCanEditAsync(Cipher cipher, Guid userId)
        {
            if(!cipher.OrganizationId.HasValue && cipher.UserId.HasValue && cipher.UserId.Value == userId)
            {
                return true;
            }

            return await _cipherRepository.GetCanEditByIdAsync(userId, cipher.Id);
        }
    }
}
