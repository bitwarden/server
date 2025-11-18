// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Services;

public interface ICipherService
{
    Task SaveAsync(Cipher cipher, Guid savingUserId, DateTime? lastKnownRevisionDate, IEnumerable<Guid> collectionIds = null,
        bool skipPermissionCheck = false, bool limitCollectionScope = true);
    Task SaveDetailsAsync(CipherDetails cipher, Guid savingUserId, DateTime? lastKnownRevisionDate,
        IEnumerable<Guid> collectionIds = null, bool skipPermissionCheck = false);
    Task<(string attachmentId, string uploadUrl)> CreateAttachmentForDelayedUploadAsync(Cipher cipher,
        string key, string fileName, long fileSize, bool adminRequest, Guid savingUserId, DateTime? lastKnownRevisionDate = null);
    Task CreateAttachmentAsync(Cipher cipher, Stream stream, string fileName, string key,
        long requestLength, Guid savingUserId, bool orgAdmin = false, DateTime? lastKnownRevisionDate = null);
    Task CreateAttachmentShareAsync(Cipher cipher, Stream stream, string fileName, string key, long requestLength,
        string attachmentId, Guid organizationShareId);
    Task DeleteAsync(CipherDetails cipherDetails, Guid deletingUserId, bool orgAdmin = false);
    Task DeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId, Guid? organizationId = null, bool orgAdmin = false);
    Task<DeleteAttachmentResponseData> DeleteAttachmentAsync(Cipher cipher, string attachmentId, Guid deletingUserId, bool orgAdmin = false);
    Task PurgeAsync(Guid organizationId);
    Task MoveManyAsync(IEnumerable<Guid> cipherIds, Guid? destinationFolderId, Guid movingUserId);
    Task SaveFolderAsync(Folder folder);
    Task DeleteFolderAsync(Folder folder);
    Task ShareAsync(Cipher originalCipher, Cipher cipher, Guid organizationId, IEnumerable<Guid> collectionIds,
        Guid userId, DateTime? lastKnownRevisionDate);
    Task<IEnumerable<CipherDetails>> ShareManyAsync(IEnumerable<(CipherDetails cipher, DateTime? lastKnownRevisionDate)> ciphers, Guid organizationId,
        IEnumerable<Guid> collectionIds, Guid sharingUserId);
    Task SaveCollectionsAsync(Cipher cipher, IEnumerable<Guid> collectionIds, Guid savingUserId, bool orgAdmin);
    Task SoftDeleteAsync(CipherDetails cipherDetails, Guid deletingUserId, bool orgAdmin = false);
    Task SoftDeleteManyAsync(IEnumerable<Guid> cipherIds, Guid deletingUserId, Guid? organizationId = null, bool orgAdmin = false);
    Task RestoreAsync(CipherDetails cipherDetails, Guid restoringUserId, bool orgAdmin = false);
    Task<ICollection<CipherOrganizationDetails>> RestoreManyAsync(IEnumerable<Guid> cipherIds, Guid restoringUserId, Guid? organizationId = null, bool orgAdmin = false);
    Task UploadFileForExistingAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentId);
    Task<AttachmentResponseData> GetAttachmentDownloadDataAsync(Cipher cipher, string attachmentId);
    Task<bool> ValidateCipherAttachmentFile(Cipher cipher, CipherAttachment.MetaData attachmentData);
    Task ValidateBulkCollectionAssignmentAsync(IEnumerable<Guid> collectionIds, IEnumerable<Guid> cipherIds, Guid userId);
}
