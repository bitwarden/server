#nullable enable
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.Repositories;

namespace Bit.Core.NotificationCenter.Repositories;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    /// <summary>
    /// Get notifications for a user with the given filter.
    /// Includes global notifications and excludes those with status (read or deleted).
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="notificationFilter">
    /// Filter for notifications.
    /// Always includes notifications with <see cref="ClientType.All"/>.
    /// Includes organizations notifications when <see cref="NotificationFilter.OrganizationIds"/> is provided.
    /// </param>
    /// <returns>
    /// Ordered by priority (highest to lowest) and creation date (descending).
    /// </returns>
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, NotificationFilter notificationFilter);

    /// <summary>
    /// Get notifications for a user with the given filters.
    /// Includes global notifications.
    /// </summary>
    /// <param name="userId">User Id</param>
    /// <param name="notificationFilter">
    /// Filter for notifications.
    /// Always includes notifications with <see cref="ClientType.All"/>.
    /// Includes organizations notifications when <see cref="NotificationFilter.OrganizationIds"/> is provided.
    /// </param>
    /// <param name="statusFilter">
    /// Filters notifications by status.
    /// If both <see cref="NotificationStatusFilter.Read"/> and <see cref="NotificationStatusFilter.Deleted"/>
    /// are false, includes notifications without a status.
    /// </param>
    /// <returns>
    /// Ordered by priority (highest to lowest) and creation date (descending).
    /// </returns>
    Task<IEnumerable<Notification>> GetByUserIdAndStatusAsync(Guid userId, NotificationFilter notificationFilter,
        NotificationStatusFilter statusFilter);
}
