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
/// Locks the access-request wire contract that the generated OpenAPI spec — and the client bindings built from it —
/// depend on. The endpoint bodies are scaffold stubs; the contract (routes, names, methods, return types) is the
/// thing under test. Endpoints are materialized by mapping them onto a minimal host and reading its
/// <see cref="EndpointDataSource"/> — the same metadata the offline OpenAPI generator inspects.
/// </summary>
public class AccessRequestEndpointsTests
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

    private static List<RouteEndpoint> AccessRequestEndpoints() =>
        MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName?.StartsWith("Pam_AccessRequests_") == true)
            .ToList();

    [Fact]
    public void MapPamEndpoints_RegistersTheSevenAccessRequestRoutes_InTheInternalDoc()
    {
        // MapPamEndpoints maps the whole PAM surface; scope to the access-request routes this contract covers.
        var endpoints = AccessRequestEndpoints();

        Assert.Equal(7, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_AccessRequests_GetInbox", "GET", "access-requests/inbox")]
    [InlineData("Pam_AccessRequests_GetHistory", "GET", "access-requests/history")]
    [InlineData("Pam_AccessRequests_GetMine", "GET", "access-requests/mine")]
    [InlineData("Pam_AccessRequests_GetDetails", "GET", "access-requests/{id:guid}")]
    [InlineData("Pam_AccessRequests_Decide", "POST", "access-requests/{id:guid}/decision")]
    [InlineData("Pam_AccessRequests_Activate", "POST", "access-requests/{id:guid}/activate")]
    [InlineData("Pam_AccessRequests_Revoke", "POST", "access-requests/{id:guid}/revoke")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes (e.g. "/access-requests/inbox")
        // that the generated spec path does not.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void AccessRequestGroup_DocumentsErrorResponseModel_For400And404()
    {
        var produces = AccessRequestEndpoints()[0].Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(nameof(AccessRequestEndpointsHandler.GetInbox), typeof(Task<ListResponseModel<AccessRequestDetailsResponseModel>>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.GetHistory), typeof(Task<ListResponseModel<AccessRequestDetailsResponseModel>>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.GetMine), typeof(Task<ListResponseModel<AccessRequestDetailsResponseModel>>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.GetDetails), typeof(Task<AccessRequestDetailsResponseModel>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.Decide), typeof(Task<AccessRequestDetailsResponseModel>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.Activate), typeof(Task<AccessLeaseResponseModel>))]
    [InlineData(nameof(AccessRequestEndpointsHandler.Revoke), typeof(Task))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(AccessRequestEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
