#nullable enable

using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Handles any requirement that implements <see cref="IOrganizationRequirement"/>.
/// Retrieves the Organization ID from the route and then passes it to the requirement's AuthorizeAsync callback to
/// determine whether the action is authorized.
/// </summary>
public class OrganizationRequirementHandler(
    IHttpContextAccessor httpContextAccessor,
    IProviderUserRepository providerUserRepository,
    IUserService userService)
    : AuthorizationHandler<IOrganizationRequirement>
{
    public const string NoHttpContextError = "This method should only be called in the context of an HTTP Request.";
    public const string NoUserIdError = "This method should only be called on the private api with a logged in user.";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IOrganizationRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException(NoHttpContextError);
        }

        var organizationId = httpContext.GetOrganizationId();
        var organizationClaims = httpContext.User.GetCurrentContextOrganization(organizationId);

        var userId = userService.GetProperUserId(httpContext.User);
        if (userId == null)
        {
            throw new InvalidOperationException(NoUserIdError);
        }

        Task<bool> IsProviderUserForOrg() => httpContext.IsProviderUserForOrgAsync(providerUserRepository, userId.Value, organizationId);

        var authorized = await requirement.AuthorizeAsync(organizationClaims, IsProviderUserForOrg);

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }
}
