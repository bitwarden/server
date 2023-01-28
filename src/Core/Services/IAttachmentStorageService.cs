using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

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
    Task<(bool, long?)> ValidateFileAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, long leeway);
}
