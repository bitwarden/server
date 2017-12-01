using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopEventService : IEventService
    {
        public Task LogCipherEventAsync(Cipher cipher, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogUserEventAsync(Guid userId, EventType type)
        {
            return Task.FromResult(0);
        }
    }
}
