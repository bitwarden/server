using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;


namespace Bit.Core.Services;

public interface IAttachmentStorageService
{
    FileUploadType FileUploadType { get; }
    Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentData);
    Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData);
    Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData);
    Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer);
    Task CleanupAsync(Guid cipherId);
    Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachmentData);
    Task DeleteAttachmentsForCipherAsync(Guid cipherId);
    Task DeleteAttachmentsForOrganizationAsync(Guid organizationId);
    Task DeleteAttachmentsForUserAsync(Guid userId);
    Task<string> GetAttachmentUploadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData);
    Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData);
    /// <summary>
    /// Parses and validates a time-limited download token, returning the cipher ID and attachment ID.
    /// Only supported by storage implementations that use signed URLs (e.g. local/self-hosted storage).
    /// </summary>
    (Guid cipherId, string attachmentId) ParseAttachmentDownloadToken(string token);
    /// <summary>
    /// Opens a read stream for a locally stored attachment file.
    /// Returns null if the storage implementation does not support direct streaming (e.g. cloud storage).
    /// </summary>
    Task<Stream?> GetAttachmentReadStreamAsync(Cipher cipher, CipherAttachment.MetaData attachmentData);
    Task<(bool, long?)> ValidateFileAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, long leeway);
}
