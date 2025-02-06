#nullable enable
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Models.Data;
using Bit.Core.NotificationCenter.Models.Filter;

namespace Bit.Core.NotificationCenter.Queries.Interfaces;

public interface IGetNotificationStatusDetailsForUserQuery
{
    Task<PagedResult<NotificationStatusDetails>> GetByUserIdStatusFilterAsync(NotificationStatusFilter statusFilter,
        PageOptions pageOptions);
}
