using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public class NoopAttachmentStorageService : IAttachmentStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task CleanupAsync(Guid cipherId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachmentData)
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

    public Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer)
    {
        return Task.FromResult(0);
    }

    public Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
    {
        return Task.FromResult(0);
    }

    public Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        return Task.FromResult(0);
    }

    public Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
    {
        return Task.FromResult(0);
    }

    public Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        return Task.FromResult((string)null);
    }

    public Task<string> GetAttachmentUploadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        return Task.FromResult(default(string));
    }
    public Task<(bool, long?)> ValidateFileAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, long leeway)
    {
        return Task.FromResult((false, (long?)null));
    }
}
