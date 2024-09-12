#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.NotificationCenter.Queries;

public class GetNotificationsForUserQuery : IGetNotificationsForUserQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationsForUserQuery(ICurrentContext currentContext,
        INotificationRepository notificationRepository)
    {
        _currentContext = currentContext;
        _notificationRepository = notificationRepository;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdStatusFilterAsync(NotificationStatusFilter statusFilter)
    {
        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var clientType = DeviceTypeToClientType(_currentContext.DeviceType);

        // Note: only returns the user's notifications - no authorization check needed
        return await _notificationRepository.GetByUserIdAndStatusAsync(_currentContext.UserId.Value, clientType,
            statusFilter);
    }

    private static ClientType DeviceTypeToClientType(DeviceType? deviceType)
    {
        return deviceType switch
        {
            not null when DeviceTypes.MobileTypes.Contains(deviceType.Value) => ClientType.Mobile,
            not null when DeviceTypes.DesktopTypes.Contains(deviceType.Value) => ClientType.Desktop,
            not null when DeviceTypes.BrowserExtensionTypes.Contains(deviceType.Value) => ClientType.Browser,
            not null when DeviceTypes.BrowserTypes.Contains(deviceType.Value) => ClientType.Web,
            _ => ClientType.All
        };
    }
}
