using System.Reflection;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.IntegrationTest.Factories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Authorization;

public class OrganizationRequirementFromRouteTests(ApiApplicationFactory factory) : IClassFixture<ApiApplicationFactory>
{
    private static readonly string[] _organizationIdRouteNames = ["orgId", "organizationId"];

    private static readonly Type[] _routeReadingRequirements =
    [
        typeof(OrgUserLinkedToUserIdRequirement),
        typeof(OrganizationCollectionManagementAccessRequirement),
    ];

    /// <summary>
    /// IF the endpoint uses:
    ///   <see cref="IOrganizationRequirement"/>
    ///   OR <see cref="OrgUserLinkedToUserIdRequirement"/>
    ///   OR <see cref="OrganizationCollectionManagementAccessRequirement"/>
    /// AND has an orgId or organizationId parameter
    /// THEN that parameter must have [FromRoute].
    ///
    /// The handler reads the id from route values only. [FromRoute] forces the binding to that.
    /// </summary>
    [Fact]
    public void AllOrganizationRequirementEndpoints_BindOrganizationIdFromRoute()
    {
        var endpointDataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var violations = endpointDataSources
            .SelectMany(source => source.Endpoints)
            .Where(HasOrganizationRequirement)
            .Where(HasOrganizationIdParameterNotBoundFromRoute)
            .Select(Describe)
            .Distinct()
            .OrderBy(description => description)
            .ToList();

        Assert.True(violations.Count == 0, BuildFailureMessage(violations));
    }

    private static bool HasOrganizationRequirement(Endpoint endpoint) =>
        endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Any(IsOrganizationRequirement);

    private static bool IsOrganizationRequirement(IAuthorizationRequirement requirement) =>
        requirement is IOrganizationRequirement || _routeReadingRequirements.Contains(requirement.GetType());

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

        var requirements = endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Where(IsOrganizationRequirement)
            .Select(requirement => requirement.GetType().Name)
            .Distinct();

        return $"{verbs} {route} [{string.Join(", ", requirements)}]";
    }

    private static string BuildFailureMessage(List<string> violations) =>
        $"{violations.Count} endpoint(s) guarded by an organization requirement declare an 'orgId' or " +
        "'organizationId' parameter without [FromRoute]. The requirement handler reads the organization id from " +
        "route values only, so a same-named parameter must be bound with [FromRoute] or a form body value can " +
        $"shadow it and diverge from the authorized id:\n  - {string.Join("\n  - ", violations)}";
}
