#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class UpdateNotificationStatusCommand : IUpdateNotificationStatusCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public UpdateNotificationStatusCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
    }

    public async Task UpdateAsync(NotificationStatus notificationStatusToUpdate)
    {
        var notification = _notificationRepository.GetByIdAsync(notificationStatusToUpdate.NotificationId);
        if (notification == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            notification, NotificationOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(
            notificationStatusToUpdate.NotificationId, notificationStatusToUpdate.UserId);
        if (notificationStatus == null)
        {
            throw new NotFoundException();
        }

        authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            notificationStatus, NotificationStatusOperations.Update);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        notificationStatus.ReadDate ??= notificationStatusToUpdate.ReadDate;
        notificationStatus.DeletedDate ??= notificationStatusToUpdate.DeletedDate;

        await _notificationStatusRepository.UpdateAsync(notificationStatus);
    }
}
