using System.Reflection;
using Bit.Scim.IntegrationTest.Factories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace Bit.Scim.IntegrationTest;

public class OrganizationIdFromRouteTests(ScimApplicationFactory factory) : IClassFixture<ScimApplicationFactory>
{
    private const string _scimPolicy = "Scim";
    private static readonly string[] _organizationIdRouteNames = ["orgId", "organizationId"];

    /// <summary>
    /// IF the endpoint is guarded by the "Scim" authorization policy
    /// AND has an orgId or organizationId parameter
    /// THEN that parameter must have [FromRoute].
    ///
    /// SCIM authorization reads the id from route values only. [FromRoute] forces the binding to that.
    /// </summary>
    [Fact]
    public void AllScimEndpoints_BindOrganizationIdFromRoute()
    {
        var endpointDataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var violations = endpointDataSources
            .SelectMany(source => source.Endpoints)
            .Where(HasScimPolicy)
            .Where(HasOrganizationIdParameterNotBoundFromRoute)
            .Select(Describe)
            .Distinct()
            .OrderBy(description => description)
            .ToList();

        Assert.True(violations.Count == 0, BuildFailureMessage(violations));
    }

    private static bool HasScimPolicy(Endpoint endpoint) =>
        endpoint.Metadata
            .OfType<IAuthorizeData>()
            .Any(data => data.Policy == _scimPolicy);

    private static bool HasOrganizationIdParameterNotBoundFromRoute(Endpoint endpoint)
    {
        var methodInfo = GetMethodInfo(endpoint);
        if (methodInfo == null)
        {
            return false;
        }

        return methodInfo.GetParameters()
            .Where(IsOrganizationIdParameter)
            .Any(parameter => parameter.GetCustomAttribute<FromRouteAttribute>() == null);
    }

    private static bool IsOrganizationIdParameter(ParameterInfo parameter) =>
        _organizationIdRouteNames.Contains(parameter.Name, StringComparer.OrdinalIgnoreCase);

    private static MethodInfo? GetMethodInfo(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.MethodInfo
        ?? endpoint.Metadata.GetMetadata<MethodInfo>();

    private static string Describe(Endpoint endpoint)
    {
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.DisplayName ?? "(unknown route)";
        var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        var verbs = httpMethods is { Count: > 0 } ? string.Join(",", httpMethods) : "ANY";

        return $"{verbs} {route}";
    }

    private static string BuildFailureMessage(List<string> violations) =>
        $"{violations.Count} SCIM endpoint(s) declare an 'orgId' or 'organizationId' parameter without [FromRoute]. " +
        "SCIM authorization reads the organization id from route values only, so a same-named parameter must be " +
        "bound with [FromRoute] or a form body value can shadow it and diverge from the authorized organization:" +
        $"\n  - {string.Join("\n  - ", violations)}";
}
