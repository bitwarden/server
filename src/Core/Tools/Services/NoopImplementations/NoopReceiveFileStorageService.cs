using Bit.Core.Enums;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public class NoopReceiveFileStorageService : IReceiveFileStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task UploadNewFileAsync(Stream stream, Receive receive, string fileId)
    {
        return Task.FromResult(0);
    }

    public Task DeleteFileAsync(Receive receive, string fileId)
    {
        return Task.FromResult(0);
    }

    public Task<string> GetReceiveFileDownloadUrlAsync(Receive receive, string fileId)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> GetReceiveFileUploadUrlAsync(Receive receive, string fileId)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<(bool valid, long length)> ValidateFileAsync(Receive receive, string fileId, long minimum, long maximum)
    {
        return Task.FromResult((false, -1L));
    }
}
