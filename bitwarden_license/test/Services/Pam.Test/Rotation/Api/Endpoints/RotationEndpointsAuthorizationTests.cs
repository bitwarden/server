using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Auth.Identity;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Authorization;
using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Api.Endpoints;

/// <summary>
/// Locks which authorization requirements the rotation routes carry. Rotation is split across two surfaces that must
/// not be gated alike: the org-scoped admin surface, authorized by <see cref="ManageRotationRequirement"/>, and the
/// daemon surface, authorized by a machine credential with no organization member behind it. Endpoints are
/// materialized by mapping them onto a minimal host and reading its <see cref="EndpointDataSource"/>.
/// </summary>
public class RotationEndpointsAuthorizationTests
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
        builder.Services.AddScoped<AuditEndpointsHandler>();
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
    public void MapPamEndpoints_GatesEveryRotationAdminRouteOnManageRotationRequirement()
    {
        // Matching on the route prefix rather than the group's tags is deliberate: it also catches a rotation admin
        // route mapped outside the group that WithPamRotationDefaults gates.
        var endpoints = AdminEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            var requirements = RequirementsFor(endpoint);
            Assert.Contains(requirements, r => r is ManageRotationRequirement);

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
    public void MapPamEndpoints_DoesNotGateTheDaemonSurfaceOnAnOrganizationRequirement()
    {
        // The daemon routes carry no {orgId}, and OrganizationRequirementHandler reads the id off the route —
        // attaching an IOrganizationRequirement here would throw rather than deny. The daemon is authorized by
        // Policies.PamRotationDaemon plus DaemonRequestEndpointFilter instead.
        var endpoints = MaterializeEndpoints()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/rotation", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            Assert.DoesNotContain(RequirementsFor(endpoint), r => r is IOrganizationRequirement);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == Policies.PamRotationDaemon);
        });
    }
}
