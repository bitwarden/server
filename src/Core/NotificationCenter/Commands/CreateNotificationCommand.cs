#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Commands;

public class CreateNotificationCommand : ICreateNotificationCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationRepository _notificationRepository;

    public CreateNotificationCommand(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationRepository notificationRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationRepository = notificationRepository;
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        notification.SetNewId();

        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User,
            notification, NotificationOperations.Create);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        return await _notificationRepository.CreateAsync(notification);
    }
}
