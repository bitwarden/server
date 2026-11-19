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
/// Locks the access-request wire contract (routes, names, methods, return types) the OpenAPI spec depends on.
/// </summary>
public class AccessRequestEndpointsTests
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
    public void MapPamEndpoints_RegistersTheSevenAccessRequestRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRequests"))
            .ToList();

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
        // The raw pattern carries leading and trailing slashes.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void AccessRequestGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRequests"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

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
