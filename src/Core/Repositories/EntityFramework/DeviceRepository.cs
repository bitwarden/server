using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class DeviceRepository : Repository<TableModel.Device, EfModel.Device, Guid>, IDeviceRepository
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

        public async Task<Device> GetByIdAsync(Guid id, Guid userId)
        {
            var device = await base.GetByIdAsync(id);
            if (device == null || device.UserId != userId)
            {
                return null;
            }

            return device;
        }

        public async Task<Device> GetByIdentifierAsync(string identifier)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.Identifier == identifier);
                return await query.FirstOrDefaultAsync();
            }
        }

        public async Task<Device> GetByIdentifierAsync(string identifier, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.Identifier == identifier && d.UserId == userId);
                return await query.FirstOrDefaultAsync();
            }
        }

        public async Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Devices.Where(d => d.UserId == userId);
                return (ICollection<Device>)await query.ToListAsync();
            }
        }
    }
}
