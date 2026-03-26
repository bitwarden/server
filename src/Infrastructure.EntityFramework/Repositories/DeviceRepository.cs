using AutoMapper;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class DeviceRepository : Repository<Core.Entities.Device, Device, Guid>, IDeviceRepository
{
    private readonly IGlobalSettings _globalSettings;

    public DeviceRepository(
        IServiceScopeFactory serviceScopeFactory,
        IMapper mapper,
        IGlobalSettings globalSettings
        )
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Devices)
    {
        _globalSettings = globalSettings;
    }

    public override async Task ReplaceAsync(Core.Entities.Device obj)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
        if (entity != null)
        {
            var mappedEntity = Mapper.Map<Device>(obj);
            dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

            // Null preserves the existing value, preventing general updates from overwriting a recently-bumped
            // LastActivityDate. Set a non-null value to update it in the same write.
            if (obj.LastActivityDate == null)
            {
                dbContext.Entry(entity).Property(d => d.LastActivityDate).IsModified = false;
            }

            await dbContext.SaveChangesAsync();
        }
    }

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

    public async Task<ICollection<DeviceAuthDetails>> GetManyByUserIdWithDeviceAuth(Guid userId)
    {
        var expirationMinutes = (int)_globalSettings.PasswordlessAuth.UserRequestExpiration.TotalMinutes;
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new DeviceWithPendingAuthByUserIdQuery();
            return await query.GetQuery(dbContext, userId, expirationMinutes).ToListAsync();
        }
    }

    public async Task BumpLastActivityDateByIdAsync(Guid deviceId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Only update if LastActivityDate has never been set or was last set on a prior calendar day.
        // This mirrors the CAST AS DATE guard in the MSSQL Device_UpdateLastActivityDateById stored procedure
        // and acts as a fallback against redundant writes if the application-layer cache is unavailable.
        // Product only requires day-level granularity (today / this week / last week / etc.).
        var now = DateTime.UtcNow;
        await dbContext.Devices
            .Where(d => d.Id == deviceId &&
                        (d.LastActivityDate == null || d.LastActivityDate.Value.Date < now.Date))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastActivityDate, now));
    }

    public async Task BumpLastActivityDateByIdentifierAndUserIdAsync(string identifier, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Identifier is unique per user, not globally (unique constraint UX_Device_UserId_Identifier
        // is on (UserId, Identifier)). Both are required for correctness and to hit the right index.
        //
        // Only update if LastActivityDate has never been set or was last set on a prior calendar day.
        // This mirrors the CAST AS DATE guard in the MSSQL Device_UpdateLastActivityDateByIdentifierUserId stored procedure
        // and acts as a fallback against redundant writes if the application-layer cache is unavailable.
        // Product only requires day-level granularity (today / this week / last week / etc.).
        var now = DateTime.UtcNow;
        await dbContext.Devices
            .Where(d => d.Identifier == identifier && d.UserId == userId &&
                        (d.LastActivityDate == null || d.LastActivityDate.Value.Date < now.Date))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastActivityDate, now));
    }

    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<Core.Entities.Device> devices)
    {
        return async (_, _) =>
        {
            var deviceUpdates = devices.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userDevices = await GetDbSet(dbContext)
                .Where(device => device.UserId == userId)
                .ToListAsync();
            var userDevicesWithUpdatesPending = userDevices
                .Where(existingDevice => deviceUpdates.Any(updatedDevice => updatedDevice.Id == existingDevice.Id))
                .ToList();

            foreach (var deviceToUpdate in userDevicesWithUpdatesPending)
            {
                var deviceUpdate = deviceUpdates.First(deviceUpdate => deviceUpdate.Id == deviceToUpdate.Id);
                deviceToUpdate.EncryptedPublicKey = deviceUpdate.EncryptedPublicKey;
                deviceToUpdate.EncryptedUserKey = deviceUpdate.EncryptedUserKey;
            }

            await dbContext.SaveChangesAsync();
        };
    }

}
