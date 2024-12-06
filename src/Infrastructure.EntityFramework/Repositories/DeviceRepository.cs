using AutoMapper;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class DeviceRepository : Repository<Core.Entities.Device, Device, Guid>, IDeviceRepository
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

    public async Task<Core.Entities.Device?> GetByIdAsync(Guid id, Guid userId)
    {
        var device = await base.GetByIdAsync(id);
        if (device == null || device.UserId != userId)
        {
            return null;
        }

        return Mapper.Map<Core.Entities.Device>(device);
    }

    public async Task<Core.Entities.Device?> GetByIdentifierAsync(string identifier)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Devices.Where(d => d.Identifier == identifier);
            var device = await query.FirstOrDefaultAsync();
            return Mapper.Map<Core.Entities.Device>(device);
        }
    }

    public async Task<Core.Entities.Device?> GetByIdentifierAsync(string identifier, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Devices.Where(d => d.Identifier == identifier && d.UserId == userId);
            var device = await query.FirstOrDefaultAsync();
            return Mapper.Map<Core.Entities.Device>(device);
        }
    }

    public async Task<ICollection<Core.Entities.Device>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Devices.Where(d => d.UserId == userId);
            var devices = await query.ToListAsync();
            return Mapper.Map<List<Core.Entities.Device>>(devices);
        }
    }

    public async Task<ICollection<DeviceAuthRequestResponseModel>> GetManyByUserIdWithDeviceAuth(Guid userId, int expirationMinutes)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = from device in dbContext.Devices
                        where device.UserId == userId && device.Active
                        select new
                        {
                            device,
                            authRequest = (from authRequest in dbContext.AuthRequests
                                           where authRequest.RequestDeviceIdentifier == device.Identifier
                                           where authRequest.Type == AuthRequestType.AuthenticateAndUnlock || authRequest.Type == AuthRequestType.Unlock
                                           where authRequest.Approved == null
                                           where authRequest.UserId == userId
                                           where authRequest.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow
                                           orderby authRequest.CreationDate descending
                                           select authRequest).First()
                        };

            var devices =
                await query.Select(deviceWithAuthRequest => new DeviceAuthRequestResponseModel(
                    deviceWithAuthRequest.device,
                    deviceWithAuthRequest.authRequest != null ? deviceWithAuthRequest.authRequest.Id : Guid.Empty,
                    deviceWithAuthRequest.authRequest != null ? deviceWithAuthRequest.authRequest.CreationDate : DateTime.MinValue)).ToListAsync();

            return devices;
        }
    }
}
