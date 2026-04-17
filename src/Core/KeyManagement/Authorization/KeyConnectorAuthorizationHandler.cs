using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.KeyManagement.Authorization;

public class KeyConnectorAuthorizationHandler : AuthorizationHandler<KeyConnectorOperationsRequirement, User>
{
    private readonly ICurrentContext _currentContext;

    public KeyConnectorAuthorizationHandler(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        KeyConnectorOperationsRequirement requirement,
        User user)
    {
        var authorized = requirement switch
        {
            not null when requirement == KeyConnectorOperations.Use => CanUse(user),
            _ => throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement))
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private bool CanUse(User user)
    {
        // User cannot use Key Connector if they already use it
        if (user.UsesKeyConnector)
        {
            return false;
        }

        // User cannot use Key Connector if they are an owner or admin of any organization
        if (_currentContext.Organizations.Any(u =>
                u.Type is OrganizationUserType.Owner or OrganizationUserType.Admin))
        {
            return false;
        }

        return true;
    }
}
