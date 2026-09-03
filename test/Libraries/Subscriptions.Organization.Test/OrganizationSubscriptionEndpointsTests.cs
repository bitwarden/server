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

public class OrganizationSubscriptionEndpointsTests
{
    [Fact]
    public void MapOrganizationSubscriptionEndpoints_AppliesAuthorizationFeatureGateAndTags()
    {
        var app = WebApplication.CreateBuilder().Build();

        // only organizationId is important for the mapping.
        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("__probe"));

        // The group requires both the authenticated Application policy and the org-billing requirement.
        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute.Policy == Policies.Application);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);

        // Feature gate: IFeatureMetadata (Bitwarden.Server.Sdk.Features) exposes a FeatureCheck
        // delegate, not a flag-name string — presence guards against a dropped RequireFeature.
        var feature = endpoint.Metadata.GetMetadata<IFeatureMetadata>();
        Assert.NotNull(feature);

        var tags = endpoint.Metadata.GetMetadata<ITagsMetadata>();
        Assert.NotNull(tags);
        Assert.Contains("OrganizationSubscriptions", tags!.Tags);

        // Group name "internal" keeps these endpoints out of the published Public API spec
        // (api.public.json); MVC controllers get this from ApiExplorerGroupConvention, but
        // Minimal API groups must set it explicitly.
        var groupName = endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>();
        Assert.NotNull(groupName);
        Assert.Equal("internal", groupName!.EndpointGroupName);
    }
}
