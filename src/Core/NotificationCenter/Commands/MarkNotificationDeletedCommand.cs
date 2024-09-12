#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class MarkNotificationDeletedCommand : IMarkNotificationDeletedCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICreateNotificationStatusCommand _createNotificationStatusCommand;
    private readonly IUpdateNotificationStatusCommand _updateNotificationStatusCommand;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public MarkNotificationDeletedCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository,
        ICreateNotificationStatusCommand createNotificationStatusCommand,
        IUpdateNotificationStatusCommand updateNotificationStatusCommand)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
        _createNotificationStatusCommand = createNotificationStatusCommand;
        _updateNotificationStatusCommand = updateNotificationStatusCommand;
    }

    public async Task MarkDeletedAsync(Guid notificationId)
    {
        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var notification = await _notificationRepository.GetByIdAsync(notificationId);
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

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(notificationId,
            _currentContext.UserId.Value);

        if (notificationStatus == null)
        {
            notificationStatus = new NotificationStatus()
            {
                NotificationId = notificationId,
                UserId = _currentContext.UserId.Value,
                DeletedDate = DateTime.Now
            };

            authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
                notificationStatus, NotificationStatusOperations.Create);
            if (!authorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }

            await _createNotificationStatusCommand.CreateAsync(notificationStatus);
        }
        else
        {
            authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
                notificationStatus, NotificationStatusOperations.Update);
            if (!authorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }

            notificationStatus.DeletedDate = DateTime.UtcNow;

            await _updateNotificationStatusCommand.UpdateAsync(notificationStatus);
        }
    }
}
