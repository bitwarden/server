#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.NotificationCenter.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class MarkNotificationReadCommand : IMarkNotificationReadCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;
    private readonly IGetNotificationStatusForUserQuery _getNotificationStatusForUserQuery;

    public MarkNotificationReadCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository,
        IGetNotificationStatusForUserQuery getNotificationStatusForUserQuery)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
        _getNotificationStatusForUserQuery = getNotificationStatusForUserQuery;
    }

    public async Task MarkReadAsync(Guid notificationId)
    {
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

        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var userId = _currentContext.UserId!.Value;

        var notificationStatus = await _getNotificationStatusForUserQuery.GetByNotificationIdAndUserIdAsync(
            notificationId, userId);

        var operationRequirement = notificationStatus == null
            ? NotificationStatusOperations.Create
            : NotificationStatusOperations.Update;

        if (notificationStatus == null)
        {
            notificationStatus = new NotificationStatus()
            {
                NotificationId = notificationId,
                UserId = userId,
                ReadDate = DateTime.Now
            };
        }

        authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            notificationStatus, operationRequirement);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        if (operationRequirement == NotificationStatusOperations.Create)
        {
            await _notificationStatusRepository.CreateAsync(notificationStatus);
        }
        else if (notificationStatus.ReadDate == null)
        {
            notificationStatus.ReadDate = DateTime.Now;
            await _notificationStatusRepository.UpdateAsync(notificationStatus);
        }
    }
}
