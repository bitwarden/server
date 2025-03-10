#nullable enable
using AutoMapper;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Repositories;

public class NotificationStatusRepository : BaseEntityFrameworkRepository, INotificationStatusRepository
{
    public NotificationStatusRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper) : base(
        serviceScopeFactory,
        mapper)
    {
    }

    public async Task<Bit.Core.NotificationCenter.Entities.NotificationStatus?> GetByNotificationIdAndUserIdAsync(Guid notificationId, Guid userId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var entity = await dbContext.NotificationStatuses
            .Where(ns =>
                ns.NotificationId == notificationId && ns.UserId == userId)
            .FirstOrDefaultAsync();

        return Mapper.Map<Bit.Core.NotificationCenter.Entities.NotificationStatus?>(entity);
    }

    public async Task<Bit.Core.NotificationCenter.Entities.NotificationStatus> CreateAsync(Bit.Core.NotificationCenter.Entities.NotificationStatus notificationStatus)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var entity = Mapper.Map<NotificationStatus>(notificationStatus);
        await dbContext.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return notificationStatus;
    }

    public async Task UpdateAsync(Bit.Core.NotificationCenter.Entities.NotificationStatus notificationStatus)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var entity = await dbContext.NotificationStatuses
            .Where(ns =>
                ns.NotificationId == notificationStatus.NotificationId && ns.UserId == notificationStatus.UserId)
            .FirstOrDefaultAsync();

        if (entity != null)
        {
            entity.DeletedDate = notificationStatus.DeletedDate;
            entity.ReadDate = notificationStatus.ReadDate;
            await dbContext.SaveChangesAsync();
        }
    }
}
