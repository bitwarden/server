using Bit.Core.Models.Api;
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
/// Locks the cipher-lease wire contract (routes, names, methods, return types) the OpenAPI spec depends on.
/// </summary>
public class CipherLeaseEndpointsTests
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
    public void MapPamEndpoints_RegistersTheThreeCipherLeaseRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("CipherLease"))
            .ToList();

        Assert.Equal(3, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_CipherLease_PreCheck", "GET", "leases/ciphers/{id:guid}/pre-check")]
    [InlineData("Pam_CipherLease_State", "GET", "leases/ciphers/{id:guid}/state")]
    [InlineData("Pam_CipherLease_Post", "POST", "leases/ciphers/{id:guid}")]
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
    public void CipherLeaseGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("CipherLease"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(nameof(CipherLeaseEndpointsHandler.PreCheck), typeof(Task<AccessPreCheckResponseModel>))]
    [InlineData(nameof(CipherLeaseEndpointsHandler.State), typeof(Task<CipherAccessStateResponseModel>))]
    [InlineData(nameof(CipherLeaseEndpointsHandler.Post), typeof(Task<AccessRequestResultResponseModel>))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(CipherLeaseEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
