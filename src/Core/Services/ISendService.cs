using System;
using System.IO;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ISendService
    {
        Task DeleteSendAsync(Send send);
        Task SaveSendAsync(Send send);
        Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);
        Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password);
        string HashPassword(string password);
        Task ValidateSendFile(Send send);

    }
}
