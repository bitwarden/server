using Bit.Api.Auth.Authorization.Requirements;
using Bit.Core.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Auth.Authorization.Handlers;

public class SameSendIdHandler : AuthorizationHandler<SameSendIdRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameSendIdRequirement requirement)
    {
        // TODO: test if this is HTTP context or not
        // https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0#access-mvc-request-context-in-handlers
        if (context.Resource is AuthorizationFilterContext mvcContext)
        {
            // TODO: discuss removal of route value completely from endpoints and just use
            // SendId claim instead

            // 1) Grab the {id} route value
            if (!mvcContext.RouteData.Values.TryGetValue("id", out var rawId))
            {
                return Task.CompletedTask;
            }

            // TODO: maybe have to handle encodedSendId

            var routeId = rawId?.ToString();
            if (string.IsNullOrEmpty(routeId))
            {
                return Task.CompletedTask;
            }

            // 2) Grab the send_id claim
            var claim = context.User.FindFirst(Claims.SendId);
            if (claim == null)
            {
                return Task.CompletedTask;
            }

            // 3) Compare them
            if (string.Equals(claim.Value, routeId, StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
