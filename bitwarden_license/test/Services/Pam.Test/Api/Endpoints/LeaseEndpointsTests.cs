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
/// Locks the lease wire contract that the generated OpenAPI spec — and the client bindings built from it —
/// depend on. The endpoint bodies are scaffold stubs; the contract (routes, names, methods, return types) is the
/// thing under test. Endpoints are materialized by mapping them onto a minimal host and reading its
/// <see cref="EndpointDataSource"/> — the same metadata the offline OpenAPI generator inspects.
/// </summary>
public class LeaseEndpointsTests
{
    private static List<RouteEndpoint> MaterializeEndpoints()
    {
        var builder = WebApplication.CreateSlimBuilder();
        // The handlers must be known services so Minimal API binding treats the handler parameter as injected
        // (not an inferred request body) — the same registration AddPamServices performs in the app.
        // MapPamEndpoints maps every PAM group, so each group's handler has to be resolvable here.
        builder.Services.AddScoped<LeaseEndpointsHandler>();
        builder.Services.AddScoped<AccessRequestEndpointsHandler>();
        builder.Services.AddScoped<AccessRuleEndpointsHandler>();

        var app = builder.Build();
        app.MapPamEndpoints();

        // Enumerating the data sources builds the endpoints — applying the route group's prefix, metadata, and
        // conventions — without starting the request pipeline, the same set the OpenAPI generator discovers.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void MapPamEndpoints_RegistersTheFiveLeaseRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("Leases"))
            .ToList();

        Assert.Equal(5, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_Leases_GetActive", "GET", "leases/active")]
    [InlineData("Pam_Leases_GetHistory", "GET", "leases/history")]
    [InlineData("Pam_Leases_GetMine", "GET", "leases/mine")]
    [InlineData("Pam_Leases_Revoke", "POST", "leases/{id:guid}/revoke")]
    [InlineData("Pam_Leases_Extend", "POST", "leases/{id:guid}/extend")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes (e.g. "/leases/active")
        // that the generated spec path does not.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void LeaseGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("Leases"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(nameof(LeaseEndpointsHandler.GetActive), typeof(Task<ListResponseModel<AccessLeaseResponseModel>>))]
    [InlineData(nameof(LeaseEndpointsHandler.GetHistory), typeof(Task<ListResponseModel<AccessLeaseResponseModel>>))]
    [InlineData(nameof(LeaseEndpointsHandler.GetMine), typeof(Task<ListResponseModel<AccessLeaseResponseModel>>))]
    [InlineData(nameof(LeaseEndpointsHandler.Revoke), typeof(Task))]
    [InlineData(nameof(LeaseEndpointsHandler.Extend), typeof(Task<AccessRequestDetailsResponseModel>))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(LeaseEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
