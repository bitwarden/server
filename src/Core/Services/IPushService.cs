using System;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface IPushService
    {
        Task PushSyncCipherCreateAsync(Cipher cipher);
        Task PushSyncCipherUpdateAsync(Cipher cipher);
        Task PushSyncCipherDeleteAsync(Cipher cipher);
        Task PushSyncCiphersAsync(Guid userId);
    }
}
