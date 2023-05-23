using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

/// <summary>
/// Combines the default <see cref="AuthorizationHandler{TRequirement}"/> and <see cref="AuthorizationHandler{TRequirement, TResource}"/>
/// implementations so that one handler can manage requirements with and without a resource.
/// </summary>
/// <remarks>
/// This is mostly for convenience to avoid declaring and registering separate handlers.
/// If your requirements always need a resource or never need a resource, use one of the default implementations.
/// </remarks>
/// <typeparam name="TRequirement">The type of the requirement to evaluate.</typeparam>
/// <typeparam name="TResource">The type of the resource to evaluate.</typeparam>
public abstract class CombinedAuthorizationHandler<TRequirement, TResource> : IAuthorizationHandler
    where TRequirement : IAuthorizationRequirement
{
    public virtual async Task HandleAsync(AuthorizationHandlerContext context)
    {
        switch (context.Resource)
        {
            case TResource resource:
                {
                    foreach (var req in context.Requirements.OfType<TRequirement>())
                        await HandleRequirementAsync(context, req, resource);

                    break;
                }
            case null:
                {
                    foreach (var req in context.Requirements.OfType<TRequirement>())
                        await HandleRequirementAsync(context, req);

                    break;
                }
        }
    }

    protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement,
        TResource resource);

    protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement);
}
