using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class DeviceRepository : Repository<TableModel.Device, EfModel.Device, Guid>, IDeviceRepository
    {
        public DeviceRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Devices)
        { }

        public Task ClearPushTokenAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<Device> GetByIdAsync(Guid id, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Device> GetByIdentifierAsync(string identifier)
        {
            throw new NotImplementedException();
        }

        public Task<Device> GetByIdentifierAsync(string identifier, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
