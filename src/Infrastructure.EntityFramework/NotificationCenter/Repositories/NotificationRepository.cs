#nullable enable
using AutoMapper;
using Bit.Core.Enums;
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
        ClientType clientType)
    {
        return await GetByUserIdAndStatusAsync(userId, clientType, new NotificationStatusFilter());
    }

    public async Task<IEnumerable<Core.NotificationCenter.Entities.Notification>> GetByUserIdAndStatusAsync(Guid userId,
        ClientType clientType, NotificationStatusFilter? statusFilter)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var notificationQuery = BuildNotificationQuery(dbContext, userId, clientType);

        if (statusFilter != null && (statusFilter.Read != null || statusFilter.Deleted != null))
        {
            notificationQuery = from n in notificationQuery
                                join ns in dbContext.NotificationStatuses on n.Id equals ns.NotificationId
                                where
                                    ns.UserId == userId &&
                                    (
                                        statusFilter.Read == null ||
                                        (statusFilter.Read == true ? ns.ReadDate != null : ns.ReadDate == null) ||
                                        statusFilter.Deleted == null ||
                                        (statusFilter.Deleted == true ? ns.DeletedDate != null : ns.DeletedDate == null)
                                    )
                                select n;
        }

        var notifications = await notificationQuery
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreationDate)
            .ToListAsync();

        return Mapper.Map<List<Core.NotificationCenter.Entities.Notification>>(notifications);
    }

    private static IQueryable<Notification> BuildNotificationQuery(DatabaseContext dbContext, Guid userId,
        ClientType clientType)
    {
        var clientTypes = new[] { ClientType.All };
        if (clientType != ClientType.All)
        {
            clientTypes = [ClientType.All, clientType];
        }

        return from n in dbContext.Notifications
               join ou in dbContext.OrganizationUsers.Where(ou => ou.UserId == userId)
                   on n.OrganizationId equals ou.OrganizationId into grouping
               from ou in grouping.DefaultIfEmpty()
               where
                   clientTypes.Contains(n.ClientType) &&
                   (
                       (
                           n.Global &&
                           n.UserId == null &&
                           n.OrganizationId == null
                       ) ||
                       (
                           !n.Global &&
                           n.UserId == userId &&
                           (n.OrganizationId == null || ou != null)
                       ) ||
                       (
                           !n.Global &&
                           n.UserId == null &&
                           ou != null
                       )
                   )
               select n;
    }
}
