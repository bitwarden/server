using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

/// <summary>
/// Allows a single authorization handler implementation to handle requirements for
/// both singular or bulk operations on single or multiple resources.
/// </summary>
/// <typeparam name="TRequirement">The type of the requirement to evaluate.</typeparam>
/// <typeparam name="TResource">The type of the resource(s) that will be evaluated.</typeparam>
public abstract class BulkAuthorizationHandler<TRequirement, TResource> : AuthorizationHandler<TRequirement>
    where TRequirement : IAuthorizationRequirement
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement)
    {
        // Attempt to get the resource(s) from the context
        var bulkResources = GetBulkResourceFromContext(context);

        // No resources of the expected type were found in the context, nothing to evaluate
        if (bulkResources == null)
        {
            return;
        }

        await HandleRequirementAsync(context, requirement, bulkResources);
    }

    private static ICollection<TResource> GetBulkResourceFromContext(AuthorizationHandlerContext context)
    {
        return context.Resource switch
        {
            TResource resource => new List<TResource> { resource },
            IEnumerable<TResource> resources => resources.ToList(),
            _ => null
        };
    }

    protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement,
        ICollection<TResource> resources);
}
