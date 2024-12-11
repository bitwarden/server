#nullable enable
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.Repositories;

namespace Bit.Core.NotificationCenter.Repositories;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    /// <summary>
    /// Get notifications for a user with the given filters.
    /// Includes global notifications.
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="clientType">
    /// Filter for notifications by client type. Always includes notifications with <see cref="ClientType.All"/>.
    /// </param>
    /// <param name="statusFilter">
    /// Filters notifications by status.
    /// If both <see cref="NotificationStatusFilter.Read"/> and <see cref="NotificationStatusFilter.Deleted"/>
    /// are not set, includes notifications without a status.
    /// </param>
    /// <returns>
    /// Ordered by priority (highest to lowest) and creation date (descending).
    /// Includes all fields from <see cref="Notification"/> and <see cref="NotificationStatus"/>
    /// </returns>
    Task<IEnumerable<NotificationStatusDetails>> GetByUserIdAndStatusAsync(
        Guid userId,
        ClientType clientType,
        NotificationStatusFilter? statusFilter
    );
}
