#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Queries.Interfaces;

public interface IGetNotificationStatusForUserQuery
{
    Task<NotificationStatus> GetByNotificationIdAndUserIdAsync(Guid notificationId);
}
