using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class NoopEventWriteService : IEventWriteService
    {
        public Task CreateAsync(IEvent e)
        {
            return Task.FromResult(0);
        }

        public Task CreateManyAsync(IList<IEvent> e)
        {
            return Task.FromResult(0);
        }
    }
}
