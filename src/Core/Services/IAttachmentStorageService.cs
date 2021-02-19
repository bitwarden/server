using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IAttachmentStorageService
    {
        Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachment);
        Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachment);
        Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData);
        Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer);
        Task CleanupAsync(Guid cipherId);
        Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachment);
        Task DeleteAttachmentsForCipherAsync(Guid cipherId);
        Task DeleteAttachmentsForOrganizationAsync(Guid organizationId);
        Task DeleteAttachmentsForUserAsync(Guid userId);
        Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData);
    }
}
