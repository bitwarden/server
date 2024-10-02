#nullable enable
using Bit.Core.Context;
using Bit.Core.NotificationCenter.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationStatusAuthorizationHandler : AuthorizationHandler<NotificationStatusOperationsRequirement,
    NotificationStatus>
{
    private readonly ICurrentContext _currentContext;

    public NotificationStatusAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        NotificationStatusOperationsRequirement requirement,
        NotificationStatus notificationStatus)
    {
        if (!_currentContext.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        var authorized = requirement switch
        {
            not null when requirement == NotificationStatusOperations.Read => CanRead(notificationStatus),
            not null when requirement == NotificationStatusOperations.Create => CanCreate(notificationStatus),
            not null when requirement == NotificationStatusOperations.Update => CanUpdate(notificationStatus),
            _ => throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement))
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool CanRead(NotificationStatus notificationStatus)
    {
        return notificationStatus.UserId == _currentContext.UserId!.Value;
    }

    private bool CanCreate(NotificationStatus notificationStatus)
    {
        return notificationStatus.UserId == _currentContext.UserId!.Value;
    }

    private bool CanUpdate(NotificationStatus notificationStatus)
    {
        return notificationStatus.UserId == _currentContext.UserId!.Value;
    }
}
