using Bit.Core.Auth.Identity;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Utilities;

/// <summary>
/// Returns 403 to organization API key tokens on any endpoint that does not require an organization scope policy.
/// </summary>
public sealed class OrganizationTokenEndpointGuardMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Authorization policy names that admit organization API key tokens.
    /// </summary>
    // Case-insensitive to match how AuthorizationOptions resolves policy names.
    public static readonly IReadOnlySet<string> OrganizationScopePolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Policies.Organization,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null &&
            IsOrganizationClient(context) &&
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null &&
            !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(a => a.Policy != null && OrganizationScopePolicies.Contains(a.Policy)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private static bool IsOrganizationClient(HttpContext context) =>
        context.User.FindFirst(JwtClaimTypes.ClientId)?.Value.StartsWith("organization.", StringComparison.Ordinal) ?? false;
}
