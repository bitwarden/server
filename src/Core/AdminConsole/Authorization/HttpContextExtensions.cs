using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Core.AdminConsole.Authorization;

public static class HttpContextExtensions
{
    public const string NoOrgIdError =
        "A route decorated with with '[Authorize<Requirement>]' must include a route value named 'orgId' or " +
        "'organizationId' either through the [Controller] attribute or through a '[Http*]' attribute.";

    private static readonly string[] _organizationIdKeys = ["orgId", "organizationId"];

    /// <summary>
    /// Parses the {orgId} or {organizationId} route parameter into a Guid, or throws if neither parameter is present
    /// or the parameter is not a valid guid.
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Guid GetOrganizationId(this HttpContext httpContext)
    {
        var routeValues = httpContext.GetRouteData().Values;

        var orgId = _organizationIdKeys
            .Select(key => routeValues.TryGetValue(key, out var value) ? value?.ToString() : null)
            .Where(value => value != null)
            .Select(value => Guid.TryParse(value, out var guid) ? guid : Guid.Empty)
            .FirstOrDefault();

        if (orgId == Guid.Empty)
        {
            throw new InvalidOperationException(NoOrgIdError);
        }

        return orgId;
    }
}
