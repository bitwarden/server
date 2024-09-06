#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Queries;

public class GetNotificationsForUserQuery : IGetNotificationsForUserQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationsForUserQuery(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdStatusFilterAsync(Guid userId,
        NotificationStatusFilter statusFilter)
    {
        var clientType = DeviceTypeToClientType(_currentContext.DeviceType);

        var notifications = await _notificationRepository.GetByUserIdAndStatusAsync(userId, clientType, statusFilter);

        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            notifications, NotificationOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        return notifications;
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
