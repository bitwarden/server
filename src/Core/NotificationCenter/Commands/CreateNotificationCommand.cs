#nullable enable
using Bit.Core.Context;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class CreateNotificationCommand : ICreateNotificationCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public CreateNotificationCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository,
        IPushNotificationService pushNotificationService)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        notification.CreationDate = notification.RevisionDate = DateTime.UtcNow;

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User, notification,
            NotificationOperations.Create);

        var newNotification = await _notificationRepository.CreateAsync(notification);

        await _pushNotificationService.PushSyncNotificationAsync(newNotification);

        return newNotification;
    }
}
