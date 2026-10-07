using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IEventWriteService
{
    Task CreateAsync(IEvent e);
    Task CreateManyAsync(IEnumerable<IEvent> e);
}
