#nullable enable
using AutoMapper;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Repositories;

public class NotificationRepository : Repository<Core.NotificationCenter.Entities.Notification, Notification, Guid>,
    INotificationRepository
{
    public NotificationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.Notifications)
    {
    }

    public async Task<IEnumerable<Core.NotificationCenter.Entities.Notification>> GetByUserIdAsync(Guid userId,
        NotificationFilter notificationFilter)
    {
        var statusFilter = new NotificationStatusFilter { Read = false, Deleted = false };
        return await GetByUserIdAndStatusAsync(userId, notificationFilter, statusFilter);
    }

    public async Task<IEnumerable<Core.NotificationCenter.Entities.Notification>> GetByUserIdAndStatusAsync(Guid userId,
        NotificationFilter notificationFilter, NotificationStatusFilter statusFilter)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var notificationStatusQuery =
            BuildNotificationQueryWithStatusFilter(dbContext, userId, notificationFilter, statusFilter);

        var notifications = await notificationStatusQuery
            .OrderBy(n => n.Priority)
            .ThenByDescending(n => n.CreationDate)
            .ToListAsync();

        return Mapper.Map<List<Core.NotificationCenter.Entities.Notification>>(notifications);
    }

    private static IQueryable<Notification> BuildNotificationQuery(DatabaseContext dbContext, Guid userId,
        NotificationFilter notificationFilter)
    {
        var clientTypes = new[] { ClientType.All };
        if (notificationFilter.ClientType != ClientType.All)
        {
            clientTypes = [ClientType.All, notificationFilter.ClientType];
        }

        if (notificationFilter.OrganizationIds != null && notificationFilter.OrganizationIds.Any())
        {
            return dbContext.Notifications
                .Where(n =>
                    clientTypes.Contains(n.ClientType) &&
                    (
                        n.UserId == userId ||
                        n.Global == true ||
                        (
                            n.OrganizationId != null &&
                            notificationFilter.OrganizationIds.Contains(n.OrganizationId.Value) &&
                            (
                                n.UserId == userId ||
                                n.UserId == null
                            )
                        )
                    )
                );
        }

        return dbContext.Notifications
            .Where(n =>
                clientTypes.Contains(n.ClientType) &&
                (
                    n.UserId == userId ||
                    n.Global == true
                )
            );
    }

    private static IQueryable<Notification> BuildNotificationQueryWithStatusFilter(
        DatabaseContext dbContext, Guid userId, NotificationFilter notificationFilter,
        NotificationStatusFilter statusFilter)
    {
        var notificationQuery = BuildNotificationQuery(dbContext, userId, notificationFilter);

        return from n in notificationQuery
               join ns in dbContext.NotificationStatuses on n.Id equals ns.NotificationId into grouping
               from ns in grouping.DefaultIfEmpty()
               where
                   (
                       ns == null &&
                       !statusFilter.Read &&
                       !statusFilter.Deleted
                   ) ||
                   (
                       ns != null &&
                       ns.UserId == userId &&
                       (statusFilter.Read ? ns.ReadDate != null : ns.ReadDate == null) &&
                       (statusFilter.Deleted ? ns.DeletedDate != null : ns.DeletedDate == null)
                   )
               select n;
    }
}
