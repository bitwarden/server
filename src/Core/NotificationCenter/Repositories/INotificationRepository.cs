#nullable enable
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.Repositories;

namespace Bit.Core.NotificationCenter.Repositories;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, NotificationFilter notificationFilter);
    Task<IEnumerable<Notification>> GetByUserIdAndStatusAsync(Guid userId, NotificationFilter notificationFilter, NotificationStatusFilter statusFilter);
}
