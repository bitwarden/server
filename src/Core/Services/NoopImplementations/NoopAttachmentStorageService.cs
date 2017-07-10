using System;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class NoopAttachmentStorageService : IAttachmentStorageService
    {
        public Task CommitShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
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

        public Task UploadNewAttachmentAsync(Stream stream, Guid cipherId, string attachmentId)
        {
            return Task.FromResult(0);
        }

        public Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId)
        {
            return Task.FromResult(0);
        }
    }
}
