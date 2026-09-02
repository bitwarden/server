using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.IntegrationTestCommon;

public abstract class RouteIdFromRouteTestsBase(IServiceProvider services)
{
    protected abstract bool IsGuardedEndpoint(Endpoint endpoint);

    protected abstract bool IsRouteIdParameter(ParameterInfo parameter);

    protected abstract string FailureSummary(int violationCount);

    protected virtual string DescribeSuffix(Endpoint endpoint) => string.Empty;

    protected void AssertAllGuardedEndpointsBindIdFromRoute()
    {
        var endpointDataSources = services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var violations = endpointDataSources
            .SelectMany(source => source.Endpoints)
            .Where(IsGuardedEndpoint)
            .Where(HasIdParameterNotBoundFromRoute)
            .Select(Describe)
            .Distinct()
            .OrderBy(description => description)
            .ToList();

        Assert.True(violations.Count == 0, BuildFailureMessage(violations));
    }

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

        return $"{verbs} {route}{DescribeSuffix(endpoint)}";
    }

    private string BuildFailureMessage(List<string> violations) =>
        $"{FailureSummary(violations.Count)}:\n  - {string.Join("\n  - ", violations)}";
}
