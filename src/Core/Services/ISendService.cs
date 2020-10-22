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
        Task CreateSendAsync(Send send, SendFileData data, Stream stream, long requestLength);
        Task<(Send, bool)> AccessAsync(Guid sendId, string password);
        string HashPassword(string password);
    }
}
