#nullable enable
using AutoMapper;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Infrastructure.EntityFramework.NotificationCenter.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.NotificationCenter.Repositories;

public class NotificationRepository : Repository<Core.NotificationCenter.Entities.Notification, Notification, Guid>, INotificationRepository
{
    public NotificationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.Notifications)
    {
    }

    public async Task<IEnumerable<Core.NotificationCenter.Entities.Notification>> GetByUserIdAsync(Guid userId,
        NotificationFilter notificationFilter)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        IQueryable<Notification> notificationQuery;

        if (notificationFilter.OrganizationIds != null && notificationFilter.OrganizationIds.Any())
        {
            notificationQuery = dbContext.Notifications
                .Where(n =>
                    (
                        n.ClientType == notificationFilter.ClientType &&
                        n.UserId == userId
                    ) ||
                    n.Global == true ||
                    (
                        n.OrganizationId != null &&
                        n.UserId == null &&
                        notificationFilter.OrganizationIds.Contains(n.OrganizationId.Value)
                    )
                );
        }
        else
        {
            notificationQuery = dbContext.Notifications
                .Where(n =>
                (
                    n.ClientType == notificationFilter.ClientType &&
                    n.UserId == userId
                ) || n.Global == true);
        }

        var notifications = await notificationQuery.OrderByDescending(c => c.CreationDate).ToListAsync();
        return Mapper.Map<List<Core.NotificationCenter.Entities.Notification>>(notifications);

    }

    public async Task<IEnumerable<Core.NotificationCenter.Entities.Notification>> GetByUserIdAndStatusAsync(Guid userId,
        NotificationFilter notificationFilter, NotificationStatusFilter statusFilter)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        IQueryable<Notification> notificationQuery;

        if (notificationFilter.OrganizationIds != null && notificationFilter.OrganizationIds.Any())
        {
            notificationQuery = dbContext.Notifications
                .Where(n =>
                    (
                        n.ClientType == notificationFilter.ClientType &&
                        n.UserId == userId
                    ) ||
                    n.Global == true ||
                    (
                        n.OrganizationId != null &&
                        n.UserId == null &&
                        notificationFilter.OrganizationIds.Contains(n.OrganizationId.Value)
                    )
                );
        }
        else
        {
            notificationQuery = dbContext.Notifications
                .Where(n =>
                (
                    n.ClientType == notificationFilter.ClientType &&
                    n.UserId == userId
                ) || n.Global == true);
        }

        notificationQuery = from n in notificationQuery
                            join ns in dbContext.NotificationStatuses on n.Id equals ns.NotificationId into grouping
                            from ns in grouping.DefaultIfEmpty()
                            where
                                  (ns.UserId == userId || ns.UserId == null) &&
                                  (statusFilter.Read == true ? ns.ReadDate != null : ns.ReadDate == null) &&
                                  (statusFilter.Deleted == true ? ns.DeletedDate != null : ns.DeletedDate == null)
                            select n;

        var notifications = await notificationQuery.OrderByDescending(c => c.CreationDate).ToListAsync();
        return Mapper.Map<List<Core.NotificationCenter.Entities.Notification>>(notifications);
    }
}
