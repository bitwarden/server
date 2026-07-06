using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Response;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints;

/// <summary>
/// Locks the access-rule wire contract that the generated OpenAPI spec — and the client bindings built from it —
/// depend on. The endpoint bodies are scaffold stubs; the contract (routes, names, methods, return types) is the
/// thing under test. Endpoints are materialized by mapping them onto a minimal host and reading its
/// <see cref="EndpointDataSource"/> — the same metadata the offline OpenAPI generator inspects.
/// </summary>
public class AccessRuleEndpointsTests
{
    private static List<RouteEndpoint> MaterializeEndpoints()
    {
        var builder = WebApplication.CreateSlimBuilder();
        // The handlers must be known services so Minimal API binding treats the handler parameter as injected
        // (not an inferred request body) — the same registration AddPamServices performs in the app.
        // MapPamEndpoints maps every PAM group, so each group's handler has to be resolvable here.
        builder.Services.AddScoped<LeaseEndpointsHandler>();
        builder.Services.AddScoped<AuditEndpointsHandler>();
        builder.Services.AddScoped<AccessRequestEndpointsHandler>();
        builder.Services.AddScoped<AccessRuleEndpointsHandler>();
        builder.Services.AddScoped<CipherLeaseEndpointsHandler>();

        var app = builder.Build();
        app.MapPamEndpoints();

        // Enumerating the data sources builds the endpoints — applying the route group's prefix, metadata, and
        // conventions — without starting the request pipeline, the same set the OpenAPI generator discovers.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static List<RouteEndpoint> AccessRuleEndpoints() =>
        MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName?.StartsWith("Pam_AccessRules_") == true)
            .ToList();

    [Fact]
    public void MapPamEndpoints_RegistersTheFiveAccessRuleRoutes_InTheInternalDoc()
    {
        // MapPamEndpoints maps the whole PAM surface; scope to the access-rule routes this contract covers.
        var endpoints = AccessRuleEndpoints();

        Assert.Equal(5, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
        Assert.All(endpoints, endpoint =>
            Assert.Contains("AccessRules", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags));
    }

    [Theory]
    [InlineData("Pam_AccessRules_GetAll", "GET", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Get", "GET", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Post", "POST", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Put", "PUT", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Delete", "DELETE", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes (e.g. "/.../access-rules/")
        // that the generated spec path does not.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void AccessRuleGroup_DocumentsErrorResponseModel_For400And404()
    {
        var produces = AccessRuleEndpoints()[0].Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(nameof(AccessRuleEndpointsHandler.GetAll), typeof(Task<ListResponseModel<AccessRuleResponseModel>>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Get), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Post), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Put), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Delete), typeof(Task))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(AccessRuleEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
