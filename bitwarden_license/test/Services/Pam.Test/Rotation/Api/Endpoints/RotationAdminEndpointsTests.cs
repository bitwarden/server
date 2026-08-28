using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Auth.Identity;
using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Authorization;
using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Api.Endpoints;

/// <summary>
/// Locks the rotation admin wire contract — fleet, target-system, and config management — that the generated OpenAPI
/// spec and the client bindings built from it depend on. The endpoint bodies are scaffold stubs; the contract
/// (routes, names, methods, return types) and the authorization the group carries are the things under test.
/// Endpoints are materialized by mapping them onto a minimal host and reading its <see cref="EndpointDataSource"/> —
/// the same metadata the offline OpenAPI generator inspects.
/// </summary>
public class RotationAdminEndpointsTests
{
    private const string AdminRoutePrefix = "/organizations/{orgId:guid}/rotation";

    private static List<RouteEndpoint> MaterializeEndpoints()
    {
        var builder = WebApplication.CreateSlimBuilder();
        // The handlers must be known services so Minimal API binding treats the handler parameter as injected
        // (not an inferred request body) — the same registration AddPamServices performs in the app.
        // MapPamEndpoints maps every PAM group, so each group's handler has to be resolvable here.
        builder.Services.AddScoped<LeaseEndpointsHandler>();
        builder.Services.AddScoped<AccessRequestEndpointsHandler>();
        builder.Services.AddScoped<AccessRuleEndpointsHandler>();
        builder.Services.AddScoped<CipherLeaseEndpointsHandler>();
        builder.Services.AddScoped<RotationDaemonEndpointsHandler>();
        builder.Services.AddScoped<RotationTargetSystemEndpointsHandler>();
        builder.Services.AddScoped<RotationConfigEndpointsHandler>();
        builder.Services.AddScoped<RotationDaemonJobsEndpointsHandler>();
        builder.Services.AddScoped<RotationJobEndpointsHandler>();
        builder.Services.AddScoped<RotationAttemptEndpointsHandler>();

        var app = builder.Build();
        app.MapPamEndpoints();

        // Enumerating the data sources builds the endpoints — applying the route group's prefix, metadata, and
        // conventions — without starting the request pipeline, the same set the OpenAPI generator discovers.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    /// <summary>
    /// Collects the authorization requirements an endpoint carries. They arrive as two shapes of metadata:
    /// <c>AuthorizeAttribute&lt;T&gt;</c> contributes <see cref="IAuthorizationRequirementData"/>, while a policy
    /// built inline contributes an <see cref="AuthorizationPolicy"/>. AuthorizationMiddleware combines both, so a
    /// test asking "what must this endpoint satisfy" has to read both.
    /// </summary>
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

        Assert.Equal(24, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_Rotation_Daemons_GetAll", "GET", "daemons")]
    [InlineData("Pam_Rotation_Daemons_Get", "GET", "daemons/{id:guid}")]
    [InlineData("Pam_Rotation_Daemons_Post", "POST", "daemons")]
    [InlineData("Pam_Rotation_Daemons_Enable", "POST", "daemons/{id:guid}/enable")]
    [InlineData("Pam_Rotation_Daemons_Disable", "POST", "daemons/{id:guid}/disable")]
    [InlineData("Pam_Rotation_Daemons_Delete", "DELETE", "daemons/{id:guid}")]
    [InlineData("Pam_Rotation_Daemons_AssignTarget", "POST", "daemons/{id:guid}/assignments")]
    [InlineData("Pam_Rotation_Daemons_UnassignTarget", "DELETE", "daemons/{id:guid}/assignments/{targetSystemId:guid}")]
    [InlineData("Pam_Rotation_TargetSystems_GetAll", "GET", "target-systems")]
    [InlineData("Pam_Rotation_TargetSystems_Post", "POST", "target-systems")]
    [InlineData("Pam_Rotation_TargetSystems_Enable", "POST", "target-systems/{id:guid}/enable")]
    [InlineData("Pam_Rotation_TargetSystems_Disable", "POST", "target-systems/{id:guid}/disable")]
    [InlineData("Pam_Rotation_TargetSystems_Rename", "PUT", "target-systems/{id:guid}/name")]
    [InlineData("Pam_Rotation_TargetSystems_UpdatePolicy", "PUT", "target-systems/{id:guid}/policy")]
    [InlineData("Pam_Rotation_Configs_GetAll", "GET", "configs")]
    [InlineData("Pam_Rotation_Configs_Get", "GET", "configs/{id:guid}")]
    [InlineData("Pam_Rotation_Configs_Post", "POST", "configs")]
    [InlineData("Pam_Rotation_Configs_PutSettings", "PUT", "configs/{id:guid}/settings")]
    [InlineData("Pam_Rotation_Configs_PutAccount", "PUT", "configs/{id:guid}/account")]
    [InlineData("Pam_Rotation_Configs_Pause", "POST", "configs/{id:guid}/pause")]
    [InlineData("Pam_Rotation_Configs_Resume", "POST", "configs/{id:guid}/resume")]
    [InlineData("Pam_Rotation_Configs_Rotate", "POST", "configs/{id:guid}/rotate")]
    [InlineData("Pam_Rotation_Configs_RecordManual", "POST", "configs/{id:guid}/record-manual")]
    [InlineData("Pam_Rotation_Configs_Delete", "DELETE", "configs/{id:guid}")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes that the generated spec path
        // does not.
        Assert.Equal($"{AdminRoutePrefix}/{route}".Trim('/'), endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void MapPamEndpoints_GatesEveryRotationAdminRouteOnManageRotationRequirement()
    {
        // Matching on the route prefix rather than the group's tags is deliberate: it also catches a rotation admin
        // route mapped outside the group that WithPamRotationDefaults gates.
        var endpoints = AdminEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            Assert.Contains(RequirementsFor(endpoint), r => r is ManageRotationRequirement);

            // The group requirement adds to Policies.Application rather than replacing it.
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == Policies.Application);
        });
    }

    [Fact]
    public void MapPamEndpoints_RotationAdminRoutesNeverAuthorizeProvidersByMembership()
    {
        // A daemon registered here is handed the organization key, which is not a provider's to hold.
        // MemberOrProviderRequirement would let them in, so no rotation admin route may carry it.
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
    [InlineData(typeof(RotationDaemonEndpointsHandler), nameof(RotationDaemonEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamDaemonResponseModel>>))]
    [InlineData(typeof(RotationDaemonEndpointsHandler), nameof(RotationDaemonEndpointsHandler.Get),
        typeof(Task<PamDaemonDetailResponseModel>))]
    [InlineData(typeof(RotationDaemonEndpointsHandler), nameof(RotationDaemonEndpointsHandler.Post),
        typeof(Task<RegisterDaemonResponseModel>))]
    [InlineData(typeof(RotationTargetSystemEndpointsHandler), nameof(RotationTargetSystemEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamTargetSystemResponseModel>>))]
    [InlineData(typeof(RotationTargetSystemEndpointsHandler), nameof(RotationTargetSystemEndpointsHandler.Post),
        typeof(Task<PamTargetSystemResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.GetAll),
        typeof(Task<ListResponseModel<PamRotationConfigResponseModel>>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.Get),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.Post),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.PutSettings),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    [InlineData(typeof(RotationConfigEndpointsHandler), nameof(RotationConfigEndpointsHandler.PutAccount),
        typeof(Task<PamRotationConfigDetailResponseModel>))]
    public void Handler_HasExpectedReturnType(Type handlerType, string methodName, Type expectedReturnType)
    {
        var method = handlerType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
