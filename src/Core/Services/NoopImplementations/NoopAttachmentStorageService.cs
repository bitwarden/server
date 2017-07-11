using System;
using System.IO;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopAttachmentStorageService : IAttachmentStorageService
    {
        public Task CleanupAsync(Guid cipherId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteAttachmentsForCipherAsync(Guid cipherId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteAttachmentsForUserAsync(Guid userId)
        {
            return Task.FromResult(0);
        }

        public Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId)
        {
            return Task.FromResult(0);
        }
    }
}
