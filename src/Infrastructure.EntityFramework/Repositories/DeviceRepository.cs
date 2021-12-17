using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class DeviceRepository : Repository<TableModel.Device, Device, Guid>, IDeviceRepository
    {
        public DeviceRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Devices)
        { }

        public async Task ClearPushTokenAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.Id == id);
                dbContext.AttachRange(query);
                await query.ForEachAsync(x => x.PushToken = null);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<TableModel.Device> GetByIdAsync(Guid id, Guid userId)
        {
            var device = await base.GetByIdAsync(id);
            if (device == null || device.UserId != userId)
            {
                return null;
            }

            return Mapper.Map<TableModel.Device>(device);
        }

        public async Task<TableModel.Device> GetByIdentifierAsync(string identifier)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.Identifier == identifier);
                var device = await query.FirstOrDefaultAsync();
                return Mapper.Map<TableModel.Device>(device);
            }
        }

        public async Task<TableModel.Device> GetByIdentifierAsync(string identifier, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.Identifier == identifier && d.UserId == userId);
                var device = await query.FirstOrDefaultAsync();
                return Mapper.Map<TableModel.Device>(device);
            }
        }

        public async Task<ICollection<TableModel.Device>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.UserId == userId);
                var devices = await query.ToListAsync();
                return Mapper.Map<List<TableModel.Device>>(devices);
            }
        }
    }
}
