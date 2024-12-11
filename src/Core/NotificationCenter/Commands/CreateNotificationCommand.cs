#nullable enable
using Bit.Core.Context;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class CreateNotificationCommand : ICreateNotificationCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;

    public CreateNotificationCommand(
        ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository
    )
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        notification.CreationDate = notification.RevisionDate = DateTime.UtcNow;

        await _authorizationService.AuthorizeOrThrowAsync(
            _currentContext.HttpContext.User,
            notification,
            NotificationOperations.Create
        );

        return await _notificationRepository.CreateAsync(notification);
    }
}
