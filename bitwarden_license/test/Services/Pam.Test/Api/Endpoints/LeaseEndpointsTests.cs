using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
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
/// Locks the lease wire contract (routes, names, methods, return types) the OpenAPI spec depends on.
/// </summary>
public class LeaseEndpointsTests
{
    private static List<RouteEndpoint> MaterializeEndpoints()
    {
        var builder = WebApplication.CreateSlimBuilder();
        // Unregistered handlers would bind as a request body.
        builder.Services.AddScoped<LeaseEndpointsHandler>();
        builder.Services.AddScoped<AccessRequestEndpointsHandler>();
        builder.Services.AddScoped<AccessRuleEndpointsHandler>();
        builder.Services.AddScoped<CipherLeaseEndpointsHandler>();
        builder.Services.AddScoped<AuditEndpointsHandler>();
        builder.Services.AddScoped<AccessConnectorEndpointsHandler>();
        builder.Services.AddScoped<TargetSystemEndpointsHandler>();
        builder.Services.AddScoped<RotationConfigEndpointsHandler>();
        builder.Services.AddScoped<RotationJobEndpointsHandler>();
        builder.Services.AddScoped<RotationAttemptEndpointsHandler>();

        var app = builder.Build();
        app.MapPamEndpoints();

        // Builds the endpoints without starting the request pipeline.
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
        // The raw pattern carries leading and trailing slashes.
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
