#nullable enable
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Queries;

public class GetNotificationStatusForUserQuery : IGetNotificationStatusForUserQuery
{
    private readonly ICurrentContext _currentContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public GetNotificationStatusForUserQuery(ICurrentContext currentContext,
        IAuthorizationService authorizationService,
        INotificationStatusRepository notificationStatusRepository)
    {
        _currentContext = currentContext;
        _authorizationService = authorizationService;
        _notificationStatusRepository = notificationStatusRepository;
    }

    public async Task<NotificationStatus> GetByNotificationIdAndUserIdAsync(Guid notificationId)
    {
        if (!_currentContext.UserId.HasValue)
        {
            throw new NotFoundException();
        }

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(notificationId,
            _currentContext.UserId.Value);
        if (notificationStatus == null)
        {
            throw new NotFoundException();
        }

        await _authorizationService.AuthorizeOrThrowAsync(_currentContext.HttpContext.User,
            notificationStatus, NotificationStatusOperations.Read);

        return notificationStatus;
    }
}
