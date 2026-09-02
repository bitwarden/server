using System.Reflection;
using Bit.Api.IntegrationTest.Factories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Authorization;

public abstract class RequirementFromRouteTestsBase(ApiApplicationFactory factory)
{
    protected abstract bool IsRequirement(IAuthorizationRequirement requirement);

    protected abstract bool IsRouteIdParameter(ParameterInfo parameter);

    protected abstract string FailureSummary(int violationCount);

    protected void AssertAllRequirementEndpointsBindIdFromRoute()
    {
        var endpointDataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var violations = endpointDataSources
            .SelectMany(source => source.Endpoints)
            .Where(HasRequirement)
            .Where(HasIdParameterNotBoundFromRoute)
            .Select(Describe)
            .Distinct()
            .OrderBy(description => description)
            .ToList();

        Assert.True(violations.Count == 0, BuildFailureMessage(violations));
    }

    private bool HasRequirement(Endpoint endpoint) =>
        endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Any(IsRequirement);

    private bool HasIdParameterNotBoundFromRoute(Endpoint endpoint)
    {
        var methodInfo = GetMethodInfo(endpoint);
        if (methodInfo == null)
        {
            return false;
        }

        return methodInfo.GetParameters()
            .Where(IsRouteIdParameter)
            .Any(parameter => parameter.GetCustomAttribute<FromRouteAttribute>() == null);
    }

    private static MethodInfo? GetMethodInfo(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.MethodInfo
        ?? endpoint.Metadata.GetMetadata<MethodInfo>();

    private string Describe(Endpoint endpoint)
    {
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.DisplayName ?? "(unknown route)";
        var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        var verbs = httpMethods is { Count: > 0 } ? string.Join(",", httpMethods) : "ANY";

        var requirements = endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Where(IsRequirement)
            .Select(requirement => requirement.GetType().Name)
            .Distinct();

        return $"{verbs} {route} [{string.Join(", ", requirements)}]";
    }

    private string BuildFailureMessage(List<string> violations) =>
        $"{FailureSummary(violations.Count)}:\n  - {string.Join("\n  - ", violations)}";
}
