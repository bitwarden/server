using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.Authorization.SecurityTasks;

public class
    SecurityTaskOrganizationAuthorizationHandler : AuthorizationHandler<SecurityTaskOperationRequirement,
    CurrentContextOrganization>
{
    private readonly ICurrentContext _currentContext;

    public SecurityTaskOrganizationAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        SecurityTaskOperationRequirement requirement,
        CurrentContextOrganization resource)
    {
        if (!_currentContext.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        var authorized = requirement switch
        {
            not null when requirement == SecurityTaskOperations.ListAllForOrganization => CanListAllTasksForOrganization(resource),
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, null)
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool CanListAllTasksForOrganization(CurrentContextOrganization org)
    {
        return org is
        { Type: OrganizationUserType.Admin or OrganizationUserType.Owner } or
        { Type: OrganizationUserType.Custom, Permissions.AccessReports: true };
    }
}
