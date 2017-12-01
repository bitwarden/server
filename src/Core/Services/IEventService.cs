using System;
using System.Threading.Tasks;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IEventService
    {
        Task LogUserEventAsync(Guid userId, EventType type);
        Task LogUserEventAsync(Guid userId, CurrentContext currentContext, EventType type);
    }
}
