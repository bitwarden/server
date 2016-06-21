using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface IDeviceRepository : IRepository<Device, Guid>
    {
        Task<Device> GetByIdAsync(Guid id, Guid userId);
        Task<Device> GetByIdentifierAsync(string identifier, Guid userId);
        Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId);
    }
}
