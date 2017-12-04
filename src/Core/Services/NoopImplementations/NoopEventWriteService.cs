using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Services
{
    public class NoopEventWriteService : IEventWriteService
    {
        public Task CreateAsync(ITableEntity entity)
        {
            return Task.FromResult(0);
        }

        public Task CreateManyAsync(IList<ITableEntity> entities)
        {
            return Task.FromResult(0);
        }
    }
}
