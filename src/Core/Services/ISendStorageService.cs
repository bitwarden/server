using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface ISendFileStorageService
    {
        FileUploadType FileUploadType { get; }
        Task UploadNewFileAsync(Stream stream, Send send, string fileId);
        Task DeleteFileAsync(Send send, string fileId);
        Task DeleteFilesForOrganizationAsync(Guid organizationId);
        Task DeleteFilesForUserAsync(Guid userId);
        Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId);
        Task<string> GetSendFileUploadUrlAsync(Send send, string fileId);
        Task<bool> ValidateFile(Send send, string fileId, long expectedFileSize, long leeway);
    }
}
