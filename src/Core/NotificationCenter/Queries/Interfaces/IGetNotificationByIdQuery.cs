#nullable enable
using Bit.Core.NotificationCenter.Entities;

namespace Bit.Core.NotificationCenter.Queries.Interfaces;

public interface IGetNotificationByIdQuery
{
    Task<Notification> GetByIdAsync(Guid notificationId);
}
