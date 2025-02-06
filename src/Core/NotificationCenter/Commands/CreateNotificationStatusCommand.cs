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

public class CreateNotificationStatusCommand : ICreateNotificationStatusCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public CreateNotificationStatusCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
    }

    public async Task<NotificationStatus> CreateAsync(NotificationStatus notificationStatus)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationStatus.NotificationId);
        if (notification == null)
        {
            throw new NotFoundException();
        }

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User, notification,
            NotificationOperations.Read);

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User, notificationStatus,
            NotificationStatusOperations.Create);

        return await _notificationStatusRepository.CreateAsync(notificationStatus);
    }
}
