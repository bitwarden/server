using System;
using System.Threading.Tasks;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public class NoopEventService : IEventService
    {
        public Task LogUserEventAsync(Guid userId, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogUserEventAsync(Guid userId, CurrentContext currentContext, EventType type)
        {
            return Task.FromResult(0);
        }
    }
}
