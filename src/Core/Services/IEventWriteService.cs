using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Services
{
    public interface IEventWriteService
    {
        Task CreateAsync(ITableEntity entity);
        Task CreateManyAsync(IList<ITableEntity> entities);
    }
}
