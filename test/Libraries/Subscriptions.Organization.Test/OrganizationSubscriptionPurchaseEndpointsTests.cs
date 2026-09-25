using Bit.Core.Auth.Identity;
using Bit.OrganizationAuthorization;
using Bit.Subscriptions.Organization.Requirements;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationSubscriptionPurchaseEndpointsTests
{
    [Fact]
    public void MapOrganizationSubscriptionPurchaseEndpoints_AppliesTheApplicationPolicyFeatureGateAndTags()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapOrganizationSubscriptionPurchaseEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = FindEndpoint(app, e => e.RoutePattern.RawText!.Contains("__probe"));

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        var authorize = Assert.Single(authorizeAttributes);
        Assert.Equal(Policies.Application, authorize.Policy);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IFeatureMetadata>());
        Assert.Contains("OrganizationSubscriptions", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()!.EndpointGroupName);
    }

    [Fact]
    public void MapOrganizationSubscriptionPurchaseEndpoints_MapsPostPurchasePreviewWithoutAnOrganizationRequirement()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapOrganizationSubscriptionPurchaseEndpoints();

        var endpoint = FindEndpoint(app,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "PreviewOrganizationSubscriptionPurchase");

        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.Equal("/purchase/preview", endpoint.RoutePattern.RawText);

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute.Policy == Policies.Application);
        Assert.DoesNotContain(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IFeatureMetadata>());
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, Func<RouteEndpoint, bool> predicate) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(predicate);
}
