﻿using AutoMapper;
using Bit.Core.Auth.Models.Data;
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
}
