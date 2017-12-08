using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class NoopEventWriteService : IEventWriteService
    {
        public Task CreateAsync(EventTableEntity entity)
        {
            return Task.FromResult(0);
        }

        public Task CreateManyAsync(IList<EventTableEntity> entities)
        {
            return Task.FromResult(0);
        }
    }
}
