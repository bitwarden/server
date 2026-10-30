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
/// Locks the access-rule wire contract (routes, names, methods, return types) the OpenAPI spec depends on.
/// </summary>
public class AccessRuleEndpointsTests
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
    public void MapPamEndpoints_RegistersTheSixAccessRuleRoutes_InTheInternalDoc()
    {
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"))
            .ToList();

        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName));
    }

    [Theory]
    [InlineData("Pam_AccessRules_GetAll", "GET", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Get", "GET", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Post", "POST", "organizations/{orgId:guid}/access-rules")]
    [InlineData("Pam_AccessRules_Put", "PUT", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_Delete", "DELETE", "organizations/{orgId:guid}/access-rules/{id:guid}")]
    [InlineData("Pam_AccessRules_GetBypassableCiphers", "GET", "organizations/{orgId:guid}/access-rules/{id:guid}/bypassable-ciphers")]
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
    public void AccessRuleGroup_DocumentsErrorResponseModel_For400And404()
    {
        var endpoint = MaterializeEndpoints()
            .First(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"));
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status400BadRequest && p.Type == typeof(ErrorResponseModel));
        Assert.Contains(produces, p => p.StatusCode == StatusCodes.Status404NotFound && p.Type == typeof(ErrorResponseModel));
    }

    /// <summary>AuthorizationMiddleware combines both metadata shapes, so both are read.</summary>
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
    // Restricted to rule managers, since it names credentials a rule fails to protect.
    [InlineData("Pam_AccessRules_GetBypassableCiphers", typeof(ManageAccessRulesRequirement))]
    public void MapPamEndpoints_AuthorizesRouteWithRequirement(string name, Type requirementType)
    {
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
        // The endpoint policy adds to the group's, so a write is never reachable on weaker terms than a read.
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
        // Access rules gate who can lease credentials, which is not a provider's to read or change.
        var endpoints = MaterializeEndpoints()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Contains("AccessRules"))
            .ToList();

        Assert.Equal(6, endpoints.Count);
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
    [InlineData(nameof(AccessRuleEndpointsHandler.GetBypassableCiphers), typeof(Task<RuleBypassableCiphersResponseModel>))]
    public void Handler_HasExpectedReturnType(string methodName, Type expectedReturnType)
    {
        var method = typeof(AccessRuleEndpointsHandler).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Equal(expectedReturnType, method!.ReturnType);
    }
}
