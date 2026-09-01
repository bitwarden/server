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

    /// <summary>
    /// Every endpoint guarded by an <see cref="IOrganizationRequirement"/> must bind the organization id from
    /// the route with an explicit <c>[FromRoute]</c> parameter named <c>orgId</c> or <c>organizationId</c>.
    /// <see cref="OrganizationRequirementHandler"/> resolves the id via
    /// <see cref="HttpContextExtensions.GetOrganizationId"/>, which reads route values only and throws
    /// <see cref="HttpContextExtensions.NoOrgIdError"/> otherwise. An endpoint that does not bind the id from
    /// the route would therefore fail at runtime. This test enforces the binding at build time.
    /// </summary>
    [Fact]
    public void AllOrganizationRequirementEndpoints_BindOrganizationIdFromRoute()
    {
        var endpointDataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var violations = endpointDataSources
            .SelectMany(source => source.Endpoints)
            .Where(HasOrganizationRequirement)
            .Where(endpoint => !BindsOrganizationIdFromRoute(endpoint))
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
            .OfType<IOrganizationRequirement>()
            .Any();

    private static bool BindsOrganizationIdFromRoute(Endpoint endpoint)
    {
        var methodInfo = GetMethodInfo(endpoint);
        if (methodInfo == null)
        {
            return false;
        }

        return methodInfo.GetParameters().Any(IsOrganizationIdFromRoute);
    }

    private static bool IsOrganizationIdFromRoute(ParameterInfo parameter)
    {
        var fromRoute = parameter.GetCustomAttribute<FromRouteAttribute>();
        if (fromRoute == null)
        {
            return false;
        }

        var boundName = fromRoute.Name ?? parameter.Name;
        return _organizationIdRouteNames.Contains(boundName, StringComparer.OrdinalIgnoreCase);
    }

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
            .OfType<IOrganizationRequirement>()
            .Select(requirement => requirement.GetType().Name)
            .Distinct();

        return $"{verbs} {route} [{string.Join(", ", requirements)}]";
    }

    private static string BuildFailureMessage(IReadOnlyCollection<string> violations) =>
        $"{violations.Count} endpoint(s) guarded by an IOrganizationRequirement do not bind the organization id " +
        "from the route. Each must declare a parameter with [FromRoute] named 'orgId' or 'organizationId' " +
        "(the OrganizationRequirementHandler reads the id from route values only and will otherwise throw at " +
        $"runtime):\n  - {string.Join("\n  - ", violations)}";
}
