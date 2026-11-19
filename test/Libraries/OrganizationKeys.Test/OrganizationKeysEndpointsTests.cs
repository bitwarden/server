using Bit.Core.Auth.Identity;
using Bit.Core.Models.Api;
using Bit.OrganizationAuthorization;
using Bit.OrganizationKeys.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.OrganizationKeys.Test;

public class OrganizationKeysEndpointsTests
{
    [Fact]
    public void MapOrganizationKeysEndpoints_AppliesAuthorizationTagsGroupNameAndExceptionHandling()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{orgId:guid}").MapOrganizationKeysEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = FindEndpoint(app, e => e.RoutePattern.RawText!.Contains("__probe"));

        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>(),
            attribute => attribute.Policy == Policies.Application);
        Assert.Contains("OrganizationKeys", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);

        // Group name "internal" keeps the endpoints out of the published Public API spec; MVC controllers get this
        // from ApiExplorerGroupConvention, but Minimal API groups must set it explicitly.
        Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()!.EndpointGroupName);

        // WithBasicExceptionHandling documents the ErrorResponseModel error responses.
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status400BadRequest
                        && metadata.Type == typeof(ErrorResponseModel));
    }

    [Fact]
    public void MapOrganizationKeysEndpoints_MapsPostSetOrganizationKeyIdForOwnersAndAdmins()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGroup("/{orgId:guid}").MapOrganizationKeysEndpoints();

        var endpoint = FindEndpoint(app,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "SetOrganizationKeyId");

        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.EndsWith("/organization-key-id", endpoint.RoutePattern.RawText);
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute.Policy == Policies.Application);
        Assert.Contains(authorizeAttributes,
            attribute => attribute is AuthorizeAttribute<OrganizationOwnerOrAdminRequirement>);
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, Func<RouteEndpoint, bool> predicate) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(predicate);
}
