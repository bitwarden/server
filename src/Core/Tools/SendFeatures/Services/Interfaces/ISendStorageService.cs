using Bit.Core.Enums;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendFileStorageService
{
    FileUploadType FileUploadType { get; }
    Task UploadNewFileAsync(Stream stream, Send send, string fileId);
    Task DeleteFileAsync(Send send, string fileId);
    Task DeleteFilesForOrganizationAsync(Guid organizationId);
    Task DeleteFilesForUserAsync(Guid userId);
    Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId);
    Task<string> GetSendFileUploadUrlAsync(Send send, string fileId);
    Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway);
}
