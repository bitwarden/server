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
            var originalLastActivityDate = entity.LastActivityDate;
            var mappedEntity = Mapper.Map<Device>(obj);
            dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

            // LastActivityDate only moves forward. Mirrors the CASE expression in Device_Update.sql:
            //   1. NULL passthrough: a general save that does not intend to touch LastActivityDate passes NULL;
            //      we must not overwrite an existing value with NULL.
            //   2. Stale non-null overwrite: a thread that loaded the device before a concurrent
            //      last-activity update fires may call ReplaceAsync with an older date; we must not
            //      clobber the fresher DB value.
            if (obj.LastActivityDate == null ||
                (originalLastActivityDate != null && obj.LastActivityDate <= originalLastActivityDate))
            {
                dbContext.Entry(entity).Property(d => d.LastActivityDate).IsModified = false;
            }

            // ClientVersion NULL passthrough: a general save that does not intend to touch ClientVersion
            // passes NULL; we must not overwrite an existing value with NULL. Downgrades are valid, so we
            // do not need the forward-only protection that LastActivityDate has.
            if (obj.ClientVersion == null)
            {
                dbContext.Entry(entity).Property(d => d.ClientVersion).IsModified = false;
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

    public async Task UpdateLastActivityByIdAsync(Guid deviceId, string? clientVersion)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // "Last activity" names the event of the device's most recent appearance. The columns
        // written here are facts we observed about that event — LastActivityDate (when) and
        // ClientVersion (what was running). ClientVersion is a property of the activity event,
        // not an independent value; future last-observed properties (e.g. last IP, OS) would slot
        // in here without renaming. See IUpdateDeviceLastActivityCommand for the contract-level note.
        //
        // Mirrors the per-column guards in Device_UpdateLastActivityById.sql. Acts as a fallback
        // against redundant writes if the application-layer cache is unavailable; the cache is the
        // primary protection. The composite Where ensures we only issue an UPDATE when at least one
        // column actually needs writing.
        //
        // Per-column semantics:
        //   - LastActivityDate: day-level idempotence (move forward to now only if today's date hasn't
        //     been recorded yet).
        //   - ClientVersion: value-level idempotence (write only when @ClientVersion is non-null and
        //     differs from the stored value). Downgrades are valid; no forward-only guard.
        var now = DateTime.UtcNow;
        var today = now.Date;

        await dbContext.Devices
            .Where(d => d.Id == deviceId
                && (
                    d.LastActivityDate == null
                    || d.LastActivityDate.Value.Date < today
                    || (clientVersion != null && (d.ClientVersion == null || d.ClientVersion != clientVersion))
                ))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastActivityDate,
                    d => (d.LastActivityDate == null || d.LastActivityDate.Value.Date < today)
                        ? now
                        : d.LastActivityDate)
                .SetProperty(d => d.ClientVersion,
                    d => (clientVersion != null && (d.ClientVersion == null || d.ClientVersion != clientVersion))
                        ? clientVersion
                        : d.ClientVersion));
    }

    public async Task UpdateLastActivityByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Identifier is unique per user, not globally (unique constraint UX_Device_UserId_Identifier
        // is on (UserId, Identifier)). Both are required for correctness and to hit the right index.
        //
        // Per-column semantics: see UpdateLastActivityByIdAsync above — same guards, different lookup key.
        var now = DateTime.UtcNow;
        var today = now.Date;

        await dbContext.Devices
            .Where(d => d.Identifier == identifier && d.UserId == userId
                && (
                    d.LastActivityDate == null
                    || d.LastActivityDate.Value.Date < today
                    || (clientVersion != null && (d.ClientVersion == null || d.ClientVersion != clientVersion))
                ))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastActivityDate,
                    d => (d.LastActivityDate == null || d.LastActivityDate.Value.Date < today)
                        ? now
                        : d.LastActivityDate)
                .SetProperty(d => d.ClientVersion,
                    d => (clientVersion != null && (d.ClientVersion == null || d.ClientVersion != clientVersion))
                        ? clientVersion
                        : d.ClientVersion));
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
