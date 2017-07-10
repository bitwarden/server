using Bit.Core.Models.Table;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IAttachmentStorageService
    {
        Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, string attachmentId);
        Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId);
        Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId);
        Task CommitShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId);
        Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId);
        Task DeleteAttachmentAsync(Guid cipherId, string attachmentId);
    }
}
