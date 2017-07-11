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
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ICollectionCipherRepository _collectionCipherRepository;
        private readonly IPushNotificationService _pushService;
        private readonly IAttachmentStorageService _attachmentStorageService;

        public CipherService(
            ICipherRepository cipherRepository,
            IFolderRepository folderRepository,
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            ICollectionCipherRepository collectionCipherRepository,
            IPushNotificationService pushService,
            IAttachmentStorageService attachmentStorageService)
        {
            _cipherRepository = cipherRepository;
            _folderRepository = folderRepository;
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _collectionCipherRepository = collectionCipherRepository;
            _pushService = pushService;
            _attachmentStorageService = attachmentStorageService;
        }

        public async Task SaveAsync(Cipher cipher, Guid savingUserId, bool orgAdmin = false)
        {
            if(!orgAdmin && !(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }

            if(cipher.Id == default(Guid))
            {
                await _cipherRepository.CreateAsync(cipher);

                // push
                await _pushService.PushSyncCipherCreateAsync(cipher);
            }
            else
            {
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher);
            }
        }

        public async Task SaveDetailsAsync(CipherDetails cipher, Guid savingUserId)
        {
            if(!(await UserCanEditAsync(cipher, savingUserId)))
            {
                throw new BadRequestException("You do not have permissions to edit this.");
            }

            cipher.UserId = savingUserId;
            if(cipher.Id == default(Guid))
            {
                await _cipherRepository.CreateAsync(cipher);

                if(cipher.OrganizationId.HasValue)
                {
                    var org = await _organizationRepository.GetByIdAsync(cipher.OrganizationId.Value);
                    cipher.OrganizationUseTotp = org.UseTotp;
                }

                // push
                await _pushService.PushSyncCipherCreateAsync(cipher);
            }
            else
            {
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher);

                // push
                await _pushService.PushSyncCipherUpdateAsync(cipher);
            }
        }

        public async Task CreateAttachmentAsync(Cipher cipher, Stream stream, string fileName, long requestLength,
            Guid savingUserId, bool orgAdmin = false)
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
                if(!user.Premium)
                {
                    throw new BadRequestException("You must be a premium user to use attachments.");
                }

                storageBytesRemaining = user.StorageBytesRemaining();
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
                cipher.AddAttachment(attachmentId, data);
            }
            catch
            {
                // Clean up since this is not transactional
                await _attachmentStorageService.DeleteAttachmentAsync(cipher.Id, attachmentId);
                throw;
            }

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher);
        }

        public async Task CreateAttachmentShareAsync(Cipher cipher, Stream stream, string fileName, long requestLength,
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

                await _attachmentStorageService.UploadShareAttachmentAsync(stream, cipher.Id, organizationId, attachmentId);
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

            // push
            await _pushService.PushSyncCipherDeleteAsync(cipher);
        }

        public async Task DeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId)
        {
            await _cipherRepository.DeleteAsync(cipherIds, deletingUserId);
            // push
            await _pushService.PushSyncCiphersAsync(deletingUserId);
        }

        public async Task DeleteAttachmentAsync(Cipher cipher, string attachmentId, Guid deletingUserId, bool orgAdmin = false)
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

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher);
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
            var hasAttachments = (attachments?.Count ?? 0) > 0;
            var storageAdjustment = attachments?.Sum(a => a.Value.Size) ?? 0;
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
                if(!org.MaxStorageGb.HasValue)
                {
                    throw new BadRequestException("This organization cannot use attachments.");
                }

                var storageBytesRemaining = org.StorageBytesRemaining();
                if(storageBytesRemaining < storageAdjustment)
                {
                    throw new BadRequestException("Not enough storage available for this organization.");
                }

                // Sproc will not save this UserId on the cipher. It is used limit scope of the collectionIds.
                cipher.UserId = sharingUserId;
                cipher.OrganizationId = organizationId;
                cipher.RevisionDate = DateTime.UtcNow;
                await _cipherRepository.ReplaceAsync(cipher, collectionIds);
                updatedCipher = true;

                if(hasAttachments)
                {
                    // migrate attachments
                    foreach(var attachment in attachments)
                    {
                        await _attachmentStorageService.StartShareAttachmentAsync(cipher.Id, organizationId, attachment.Key);
                        migratedAttachments = true;
                    }
                }
            }
            catch
            {
                // roll everything back
                if(updatedCipher)
                {
                    await _cipherRepository.ReplaceAsync(originalCipher);
                }

                if(!hasAttachments || !migratedAttachments)
                {
                    throw;
                }

                if(updatedCipher)
                {
                    await _userRepository.UpdateStorageAsync(sharingUserId, storageAdjustment);
                    await _organizationRepository.UpdateStorageAsync(organizationId, -1 * storageAdjustment);
                }

                foreach(var attachment in attachments)
                {
                    await _attachmentStorageService.RollbackShareAttachmentAsync(cipher.Id, organizationId, attachment.Key);
                }

                await _attachmentStorageService.CleanupAsync(cipher.Id);
                throw;
            }

            // commit attachment migration
            await _attachmentStorageService.CleanupAsync(cipher.Id);

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher);
        }

        public async Task SaveCollectionsAsync(Cipher cipher, IEnumerable<Guid> collectionIds, Guid savingUserId, bool orgAdmin)
        {
            if(cipher.Id == default(Guid))
            {
                throw new BadRequestException(nameof(cipher.Id));
            }

            if(!cipher.OrganizationId.HasValue)
            {
                throw new BadRequestException("Cipher must belong to an organization.");
            }

            // The sprocs will validate that all collections belong to this org/user and that they have proper write permissions.
            if(orgAdmin)
            {
                await _collectionCipherRepository.UpdateCollectionsForAdminAsync(cipher.Id, cipher.OrganizationId.Value,
                    collectionIds);
            }
            else
            {
                await _collectionCipherRepository.UpdateCollectionsAsync(cipher.Id, savingUserId, collectionIds);
            }

            // push
            await _pushService.PushSyncCipherUpdateAsync(cipher);
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
