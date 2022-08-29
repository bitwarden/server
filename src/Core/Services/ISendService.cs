using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface ISendService
{
    Task DeleteSendAsync(Send send);
    Task SaveSendAsync(Send send);
    Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);
    Task UploadFileToExistingSendAsync(Stream stream, Send send);
    Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password);
    string HashPassword(string password);
    Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
    Task<bool> ValidateSendFile(Send send);
}
