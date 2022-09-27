using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Services;

public class NoopSendFileStorageService : ISendFileStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task UploadNewFileAsync(Stream stream, Send send, string attachmentId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteFileAsync(Send send, string fileId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteFilesForOrganizationAsync(Guid organizationId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteFilesForUserAsync(Guid userId)
    {
        return Task.FromResult(0);
    }

    public Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId)
    {
        return Task.FromResult((string)null);
    }

    public Task<string> GetSendFileUploadUrlAsync(Send send, string fileId)
    {
        return Task.FromResult((string)null);
    }

    public Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway)
    {
        return Task.FromResult((false, default(long?)));
    }
}
