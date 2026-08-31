using Bit.Core.Auth.Identity;
using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Authorization;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints;

/// <summary>
/// Pins the audit endpoints' route, name, method, and return type.
/// </summary>
public class AuditEndpointsTests
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

    // Two reads over the one resource: the trail itself, and the subjects it names -- which is what the Item filter's
    // menu is built from, and cannot be derived from a page of the trail.
    [Fact]
    public void MapPamEndpoints_RegistersTheAuditRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("Audit"))
            .ToList();

        Assert.Equal(2, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_Audit_GetTrail", "GET", "organizations/{orgId:guid}/audit")]
    [InlineData("Pam_Audit_GetItems", "GET", "organizations/{orgId:guid}/audit/items")]
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

    private static List<IAuthorizationRequirement> RequirementsFor(Endpoint endpoint) =>
    [
        .. endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().SelectMany(policy => policy.Requirements),
        .. endpoint.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>().SelectMany(data => data.GetRequirements())
    ];

    [Theory]
    [InlineData("Pam_Audit_GetTrail")]
    [InlineData("Pam_Audit_GetItems")]
    public void MapPamEndpoints_AuthorizesAuditRoutesWithAccessAuditTrailRequirement(string name)
    {
        var endpoint = Assert.Single(
            MaterializeEndpoints(),
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);

        Assert.Contains(RequirementsFor(endpoint), r => r is AccessAuditTrailRequirement);
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == Policies.Application);
    }

    [Fact]
    public void AuditGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("Audit"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(nameof(AuditEndpointsHandler.GetTrail), typeof(Task<ListResponseModel<AccessAuditEventResponseModel>>))]
    [InlineData(nameof(AuditEndpointsHandler.GetItems), typeof(Task<ListResponseModel<AccessAuditItemResponseModel>>))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(AuditEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
