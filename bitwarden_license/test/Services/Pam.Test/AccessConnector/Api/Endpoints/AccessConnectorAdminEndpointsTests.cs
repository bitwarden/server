using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Auth.Identity;
using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Authorization;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Api.Models.Response;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Endpoints;

/// <summary>
/// Locks the rotation admin wire contract (routes, names, methods, return types) the OpenAPI spec depends on, and the
/// authorization the group carries.
/// </summary>
public class AccessConnectorAdminEndpointsTests
{
    private const string AdminRoutePrefix = "/organizations/{orgId:guid}/access-connectors";

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

    /// <summary>AuthorizationMiddleware combines both metadata shapes, so both are read.</summary>
    private static List<IAuthorizationRequirement> RequirementsFor(Endpoint endpoint) =>
    [
        .. endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().SelectMany(policy => policy.Requirements),
        .. endpoint.Metadata.GetOrderedMetadata<IAuthorizationRequirementData>().SelectMany(data => data.GetRequirements())
    ];

    private static List<RouteEndpoint> AdminEndpoints() =>
        MaterializeEndpoints()
            .Where(e => e.RoutePattern.RawText!.StartsWith(AdminRoutePrefix, StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void MapPamEndpoints_RegistersEveryRotationAdminRoute_InTheInternalDoc()
    {
        var endpoints = AdminEndpoints();

        Assert.Equal(23, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_AccessConnectors_GetAll", "GET", "")]
    [InlineData("Pam_AccessConnectors_Get", "GET", "{id:guid}")]
    [InlineData("Pam_AccessConnectors_Post", "POST", "")]
    [InlineData("Pam_AccessConnectors_Enable", "POST", "{id:guid}/enable")]
    [InlineData("Pam_AccessConnectors_Disable", "POST", "{id:guid}/disable")]
    [InlineData("Pam_AccessConnectors_Delete", "DELETE", "{id:guid}")]
    [InlineData("Pam_AccessConnectors_AssignTarget", "POST", "{id:guid}/assignments")]
    [InlineData("Pam_AccessConnectors_UnassignTarget", "DELETE", "{id:guid}/assignments/{targetSystemId:guid}")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_GetAll", "GET", "rotation/target-systems")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_Post", "POST", "rotation/target-systems")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_Enable", "POST", "rotation/target-systems/{id:guid}/enable")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_Disable", "POST", "rotation/target-systems/{id:guid}/disable")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_Put", "PUT", "rotation/target-systems/{id:guid}")]
    [InlineData("Pam_AccessConnectors_Rotation_TargetSystems_Delete", "DELETE", "rotation/target-systems/{id:guid}")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_GetAll", "GET", "rotation/configs")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Get", "GET", "rotation/configs/{id:guid}")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Post", "POST", "rotation/configs")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Put", "PUT", "rotation/configs/{id:guid}")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Pause", "POST", "rotation/configs/{id:guid}/pause")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Resume", "POST", "rotation/configs/{id:guid}/resume")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Rotate", "POST", "rotation/configs/{id:guid}/rotate")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_RecordManual", "POST",
        "rotation/configs/{id:guid}/record-manual")]
    [InlineData("Pam_AccessConnectors_Rotation_Configs_Delete", "DELETE", "rotation/configs/{id:guid}")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // The raw pattern carries leading and trailing slashes.
        Assert.Equal($"{AdminRoutePrefix}/{route}".Trim('/'), endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void MapPamEndpoints_GatesEveryRotationAdminRouteOnManageRotationRequirement()
    {
        // Matched by route prefix rather than tags, so an admin route mapped outside the gated group is caught too.
        var endpoints = AdminEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            Assert.Contains(RequirementsFor(endpoint), r => r is ManageAccessConnectorRequirement);

            // The group requirement adds to Policies.Application rather than replacing it.
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == Policies.Application);
        });
    }

    [Fact]
    public void MapPamEndpoints_RotationAdminRoutesNeverAuthorizeProvidersByMembership()
    {
        // A registered access connector is handed the organization key, which is not a provider's to hold.
        Assert.All(AdminEndpoints(), endpoint =>
        {
            var requirements = RequirementsFor(endpoint);
            Assert.DoesNotContain(requirements, r => r is MemberOrProviderRequirement);
            Assert.DoesNotContain(requirements, r => r is MemberRequirement);
        });
    }

    [Fact]
    public void RotationAdminGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = AdminEndpoints().First();
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(typeof(AccessConnectorEndpointsHandler), nameof(AccessConnectorEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamAccessConnectorResponseModel>>))]
    [InlineData(typeof(AccessConnectorEndpointsHandler), nameof(AccessConnectorEndpointsHandler.Get),
        typeof(Task<PamAccessConnectorDetailResponseModel>))]
    [InlineData(typeof(AccessConnectorEndpointsHandler), nameof(AccessConnectorEndpointsHandler.Post),
        typeof(Task<RegisterAccessConnectorResponseModel>))]
    [InlineData(typeof(TargetSystemEndpointsHandler), nameof(TargetSystemEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamTargetSystemResponseModel>>))]
    [InlineData(typeof(TargetSystemEndpointsHandler), nameof(TargetSystemEndpointsHandler.Post),
        typeof(Task<PamTargetSystemResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamRotationConfigResponseModel>>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.Get),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.Post),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.Put),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    public void Handler_HasExpectedReturnType(Type handlerType, string methodName, Type expectedReturnType)
    {
        var method = handlerType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
