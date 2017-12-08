using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public interface IEventWriteService
    {
        Task CreateAsync(EventTableEntity entity);
        Task CreateManyAsync(IList<EventTableEntity> entities);
    }
}
