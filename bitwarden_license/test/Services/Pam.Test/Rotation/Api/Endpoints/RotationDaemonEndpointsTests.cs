using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Models.Api;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
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
/// Locks the daemon-facing rotation wire contract that the generated OpenAPI spec — and the rotation daemon built
/// from it — depend on. The endpoint bodies are scaffold stubs; the contract (routes, names, methods, return types,
/// and the machine-credential policy) is the thing under test. Endpoints are materialized by mapping them onto a
/// minimal host and reading its <see cref="EndpointDataSource"/> — the same metadata the offline OpenAPI generator
/// inspects.
/// </summary>
public class RotationDaemonEndpointsTests
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

    private static List<RouteEndpoint> DaemonEndpoints() =>
        MaterializeEndpoints()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/rotation", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void MapPamEndpoints_RegistersTheSixDaemonRoutes_InTheInternalDoc()
    {
        var endpoints = DaemonEndpoints();

        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_Rotation_DaemonJobs_GetAll", "GET", "rotation/daemon/jobs")]
    [InlineData("Pam_Rotation_Jobs_Claim", "POST", "rotation/jobs/{id:guid}/claim")]
    [InlineData("Pam_Rotation_Attempts_GetCipher", "GET", "rotation/attempts/{id:guid}/cipher")]
    [InlineData("Pam_Rotation_Attempts_PutCipher", "PUT", "rotation/attempts/{id:guid}/cipher")]
    [InlineData("Pam_Rotation_Attempts_Success", "POST", "rotation/attempts/{id:guid}/success")]
    [InlineData("Pam_Rotation_Attempts_Failure", "POST", "rotation/attempts/{id:guid}/failure")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes (e.g. "/rotation/daemon/jobs")
        // that the generated spec path does not.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void MapPamEndpoints_DoesNotGateTheDaemonSurfaceOnAnOrganizationRequirement()
    {
        // The daemon routes carry no {orgId}, and OrganizationRequirementHandler reads the id off the route —
        // attaching an IOrganizationRequirement here would throw rather than deny. A daemon is scoped to its own
        // organization by its token and by the queries underneath, so this holds once the machine-credential
        // policy replaces the placeholder Policies.Application these routes carry today.
        var endpoints = DaemonEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.DoesNotContain(RequirementsFor(endpoint), r => r is IOrganizationRequirement));
    }

    [Fact]
    public void DaemonGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = DaemonEndpoints().First();
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    [Theory]
    [InlineData(typeof(RotationDaemonJobsEndpointsHandler), nameof(RotationDaemonJobsEndpointsHandler.GetJobs),
        typeof(Task<ListResponseModel<ClaimableRotationJobResponseModel>>))]
    [InlineData(typeof(RotationJobEndpointsHandler), nameof(RotationJobEndpointsHandler.Claim),
        typeof(Task<RotationClaimResponseModel>))]
    [InlineData(typeof(RotationAttemptEndpointsHandler), nameof(RotationAttemptEndpointsHandler.GetCipher),
        typeof(Task<RotationCipherResponseModel>))]
    [InlineData(typeof(RotationAttemptEndpointsHandler), nameof(RotationAttemptEndpointsHandler.PutCipher),
        typeof(Task))]
    [InlineData(typeof(RotationAttemptEndpointsHandler), nameof(RotationAttemptEndpointsHandler.Success),
        typeof(Task))]
    [InlineData(typeof(RotationAttemptEndpointsHandler), nameof(RotationAttemptEndpointsHandler.Failure),
        typeof(Task))]
    public void Handler_HasExpectedReturnType(Type handlerType, string methodName, Type expectedReturnType)
    {
        var method = handlerType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
