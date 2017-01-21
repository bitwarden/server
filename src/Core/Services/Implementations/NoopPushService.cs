using System;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public class NoopPushService : IPushService
    {
        public Task PushSyncCipherCreateAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCiphersAsync(Guid userId)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCipherUpdateAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }
    }
}
