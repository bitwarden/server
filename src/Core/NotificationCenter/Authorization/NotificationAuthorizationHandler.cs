#nullable enable
using Bit.Core.Context;
using Bit.Core.NotificationCenter.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.NotificationCenter.Authorization;

public class NotificationAuthorizationHandler : AuthorizationHandler<NotificationOperationsRequirement, Notification>
{
    private readonly ICurrentContext _currentContext;

    public NotificationAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        NotificationOperationsRequirement requirement,
        Notification notification)
    {
        if (!_currentContext.UserId.HasValue)
        {
            return;
        }

        var authorized = requirement switch
        {
            not null when requirement == NotificationOperations.Read => CanRead(notification),
            not null when requirement == NotificationOperations.Create => await CanCreate(notification),
            not null when requirement == NotificationOperations.Update => await CanUpdate(notification),
            _ => throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement))
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private bool CanRead(Notification notification)
    {
        var userMatching = !notification.UserId.HasValue || notification.UserId.Value == _currentContext.UserId!.Value;
        var organizationMatching = !notification.OrganizationId.HasValue ||
                                   _currentContext.GetOrganization(notification.OrganizationId.Value) != null;

        return notification.Global || (userMatching && organizationMatching);
    }

    private async Task<bool> CanCreate(Notification notification)
    {
        var organizationPermissionsMatching = !notification.OrganizationId.HasValue ||
                                              await _currentContext.AccessReports(notification.OrganizationId.Value);
        var userNoOrganizationMatching = !notification.UserId.HasValue || notification.OrganizationId.HasValue ||
                                         notification.UserId.Value == _currentContext.UserId!.Value;

        return !notification.Global && organizationPermissionsMatching && userNoOrganizationMatching;
    }

    private async Task<bool> CanUpdate(Notification notification)
    {
        var organizationPermissionsMatching = !notification.OrganizationId.HasValue ||
                                              await _currentContext.AccessReports(notification.OrganizationId.Value);
        var userNoOrganizationMatching = !notification.UserId.HasValue || notification.OrganizationId.HasValue ||
                                         notification.UserId.Value == _currentContext.UserId!.Value;

        return !notification.Global && organizationPermissionsMatching && userNoOrganizationMatching;
    }
}
