using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public interface IEventWriteService
    {
        Task CreateAsync(IEvent e);
        Task CreateManyAsync(IList<IEvent> e);
    }
}
