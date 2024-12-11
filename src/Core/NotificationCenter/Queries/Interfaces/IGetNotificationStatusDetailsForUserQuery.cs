#nullable enable
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Core.NotificationCenter.Models.Filter;

namespace Bit.Core.NotificationCenter.Queries.Interfaces;

public interface IGetNotificationStatusDetailsForUserQuery
{
    Task<IEnumerable<NotificationStatusDetails>> GetByUserIdStatusFilterAsync(
        NotificationStatusFilter statusFilter
    );
}
