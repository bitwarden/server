#nullable enable
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Models.Filter;

namespace Bit.Core.NotificationCenter.Queries.Interfaces;

public interface IGetNotificationStatusDetailsForUserQuery
{
    Task<IEnumerable<NotificationStatusDetails>> GetByUserIdStatusFilterAsync(NotificationStatusFilter statusFilter);
}
