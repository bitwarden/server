#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Repositories;

public interface INotificationStatusRepository
{
    Task<NotificationStatus?> GetByNotificationIdAndUserIdAsync(Guid notificationId, Guid userId);
    Task<NotificationStatus> CreateAsync(NotificationStatus notificationStatus);
    Task UpdateAsync(NotificationStatus notificationStatus);
}
