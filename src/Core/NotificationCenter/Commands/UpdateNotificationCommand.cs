#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class UpdateNotificationCommand : IUpdateNotificationCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;

    public UpdateNotificationCommand(
        ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository
    )
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
    }

    public async Task UpdateAsync(Notification notificationToUpdate)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationToUpdate.Id);
        if (notification == null)
        {
            throw new NotFoundException();
        }

        await _authorizationService.AuthorizeOrThrowAsync(
            _currentContext.HttpContext.User,
            notification,
            NotificationOperations.Update
        );

        notification.Priority = notificationToUpdate.Priority;
        notification.ClientType = notificationToUpdate.ClientType;
        notification.Title = notificationToUpdate.Title;
        notification.Body = notificationToUpdate.Body;
        notification.RevisionDate = DateTime.UtcNow;

        await _notificationRepository.ReplaceAsync(notification);
    }
}
