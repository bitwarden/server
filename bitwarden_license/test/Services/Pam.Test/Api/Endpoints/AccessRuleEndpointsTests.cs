using Bit.Api.AdminConsole.Authorization.Requirements;
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
/// Locks the access-rule wire contract that the generated OpenAPI spec — and the client bindings built from it —
/// depend on. The endpoint bodies are scaffold stubs; the contract (routes, names, methods, return types) is the
/// thing under test. Endpoints are materialized by mapping them onto a minimal host and reading its
/// <see cref="EndpointDataSource"/> — the same metadata the offline OpenAPI generator inspects.
/// </summary>
public class AccessRuleEndpointsTests
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
        builder.Services.AddScoped<AccessConnectorEndpointsHandler>();
        builder.Services.AddScoped<TargetSystemEndpointsHandler>();
        builder.Services.AddScoped<RotationConfigEndpointsHandler>();
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

    [Fact]
    public void MapPamEndpoints_RegistersTheFiveAccessRuleRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"))
            .ToList();

        Assert.Equal(5, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_AccessRules_GetAll", "GET", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Get", "GET", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Post", "POST", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Put", "PUT", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Delete", "DELETE", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    public void MapPamEndpoints_RegistersExpectedRoute(string name, string method, string route)
    {
        var endpoints = MaterializeEndpoints();

        var endpoint = Assert.Single(
            endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
        // Trim slashes: the raw pattern carries routing's leading/trailing slashes (e.g. "/.../access-rules/")
        // that the generated spec path does not.
        Assert.Equal(route, endpoint.RoutePattern.RawText?.Trim('/'));
        Assert.Contains(method, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

    [Fact]
    public void AccessRuleGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
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

    [Theory]
    [InlineData("Pam_AccessRules_GetAll", typeof(MemberRequirement))]
    [InlineData("Pam_AccessRules_Get", typeof(MemberRequirement))]
    [InlineData("Pam_AccessRules_Post", typeof(ManageAccessRulesRequirement))]
    [InlineData("Pam_AccessRules_Put", typeof(ManageAccessRulesRequirement))]
    [InlineData("Pam_AccessRules_Delete", typeof(ManageAccessRulesRequirement))]
    public void MapPamEndpoints_AuthorizesRouteWithRequirement(string name, Type requirementType)
    {
        // Reads require membership; writes require authority over rule authorship. The requirements are carried as
        // endpoint metadata, which AuthorizationMiddleware combines with the group's Policies.Application.
        var endpoint = Assert.Single(
            MaterializeEndpoints(),
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);

        Assert.Contains(RequirementsFor(endpoint), r => r.GetType() == requirementType);

        // The per-route requirement adds to the group's policy rather than replacing it.
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == Policies.Application);
    }

    [Fact]
    public void MapPamEndpoints_AccessRuleWritesRequireMembershipBesidesThePermission()
    {
        // A write must satisfy the group's MemberRequirement *in addition to* the permission: the endpoint policy
        // adds to the group policy rather than replacing it, so a write is never reachable on weaker terms than a
        // read. ManageAccessRulesRequirement independently excludes providers — see
        // ManageAccessRulesRequirementTests.
        var writeRoutes = new[] { "Pam_AccessRules_Post", "Pam_AccessRules_Put", "Pam_AccessRules_Delete" };

        var endpoints = MaterializeEndpoints()
            .Where(e => writeRoutes.Contains(e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName))
            .ToList();

        Assert.Equal(writeRoutes.Length, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            var requirements = RequirementsFor(endpoint);
            Assert.Contains(requirements, r => r is MemberRequirement);
            Assert.Contains(requirements, r => r is ManageAccessRulesRequirement);
        });
    }

    [Fact]
    public void MapPamEndpoints_AccessRulesNeverAuthorizeProvidersByMembership()
    {
        // Access rules gate who can lease credentials out of an organization, which is not a provider's to read or
        // change. MemberOrProviderRequirement would let them in, so no access-rule route may carry it.
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"))
            .ToList();

        Assert.Equal(5, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            var requirements = RequirementsFor(endpoint);
            Assert.Contains(requirements, r => r is MemberRequirement);
            Assert.DoesNotContain(requirements, r => r is MemberOrProviderRequirement);
        });
    }

    [Theory]
    [InlineData(nameof(AccessRuleEndpointsHandler.GetAll), typeof(Task<ListResponseModel<AccessRuleResponseModel>>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Get), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Post), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Put), typeof(Task<AccessRuleResponseModel>))]
    [InlineData(nameof(AccessRuleEndpointsHandler.Delete), typeof(Task))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(AccessRuleEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
