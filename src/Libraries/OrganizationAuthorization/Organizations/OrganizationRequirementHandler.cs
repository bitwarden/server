#nullable enable

using Bit.Core.AdminConsole.Repositories;
using Bit.CurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Handles any requirement that implements <see cref="IOrganizationRequirement"/>.
/// Retrieves the Organization ID from the route and then passes it to the requirement's AuthorizeAsync callback to
/// determine whether the action is authorized.
/// </summary>
public class OrganizationRequirementHandler(
    IHttpContextAccessor httpContextAccessor,
    IProviderUserRepository providerUserRepository,
    ICurrentUserContext currentUserContext)
    : AuthorizationHandler<IOrganizationRequirement>
{
    public const string NoHttpContextError = "This method should only be called in the context of an HTTP Request.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException(NoHttpContextError);
        }

        // Without a user id there is nobody to authorize. Leave the requirement unmet rather than throwing, so the
        // authorization middleware responds with 401 for anonymous callers and 403 for other principals.
        if (!currentUserContext.TryGetUserId(out var userId))
        {
            return;
        }

        var parsedOrganizationId = httpContext.TryGetOrganizationId();
        if (parsedOrganizationId is not { } organizationId)
        {
            // A route value that is present but not a Guid is a bad caller, not a bug in the route: deny the request.
            // A route with no organization id at all is a programming error and must still fail loudly.
            if (httpContext.HasOrganizationIdRouteValue())
            {
                return;
            }

            throw new InvalidOperationException(HttpContextExtensions.NoOrgIdError);
        }

        var organizationClaims = httpContext.User.GetCurrentContextOrganization(organizationId);

        Task<bool> IsProviderUserForOrg() => httpContext.IsProviderUserForOrgAsync(providerUserRepository, userId, organizationId);

        var authorized = await requirement.AuthorizeAsync(organizationClaims, IsProviderUserForOrg);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
