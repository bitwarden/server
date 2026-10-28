using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Invoicing;
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
    public void MapOrganizationSubscriptionEndpoints_AppliesAuthorizationAndTagsButNoFeatureGate()
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

        // The group carries no feature gate: each endpoint declares its own, so a flag for one kind of endpoint
        // (previews, a purchase) never gates another.
        Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IFeatureMetadata>());

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

    [Fact]
    public void MapOrganizationSubscriptionEndpoints_PreviewRequiresStandaloneOrganizationOwner()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();

        var preview = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.EndsWith("/preview")
                         && !e.RoutePattern.RawText!.Contains("plan-change")
                         && !e.RoutePattern.RawText!.Contains("restart"));

        var authorizeAttributes = preview.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);
    }

    [Fact]
    public void MapOrganizationSubscriptionEndpoints_PlanChangePreviewIsPostAndRequiresStandaloneOrganizationOwner()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();

        var planChange = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("plan-change/preview"));

        var methods = planChange.Metadata.GetMetadata<IHttpMethodMetadata>();
        Assert.NotNull(methods);
        Assert.Contains("POST", methods!.HttpMethods);

        var authorizeAttributes = planChange.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);
    }

    [Fact]
    public void MapOrganizationSubscriptionEndpoints_RestartPreviewIsPostAndInheritsOrganizationBillingOnly()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();

        var restartPreview = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("restart/preview"));

        var methods = restartPreview.Metadata.GetMetadata<IHttpMethodMetadata>();
        Assert.NotNull(methods);
        Assert.Contains("POST", methods!.HttpMethods);

        var authorizeAttributes = restartPreview.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.DoesNotContain(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);

        var name = restartPreview.Metadata.GetMetadata<IEndpointNameMetadata>();
        Assert.NotNull(name);
        Assert.Equal("PreviewOrganizationRestart", name!.EndpointName);
    }

    [Fact]
    public void MapOrganizationSubscriptionEndpoints_PurchasePrivilegedControlsIsPostRequiresStandaloneOwnerAndPamFlag()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
        var purchase = endpoints.Single(e => e.RoutePattern.RawText!.Contains("privileged-controls/purchase"));

        var methods = purchase.Metadata.GetMetadata<IHttpMethodMetadata>();
        Assert.NotNull(methods);
        Assert.Contains("POST", methods!.HttpMethods);

        var authorizeAttributes = purchase.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);

        // Gated by the Privileged Controls flag alone, not by the preview flag the preview endpoints carry.
        var feature = Assert.Single(purchase.Metadata.GetOrderedMetadata<IFeatureMetadata>());
        Assert.Contains(FeatureFlagKeys.PM39883_Pam_Billing_Support, feature.ToString());
        Assert.DoesNotContain(InvoicingFeatureFlags.PM36631_PreviewDrivenCart, feature.ToString());
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("plan-change/preview")]
    [InlineData("restart/preview")]
    public void MapOrganizationSubscriptionEndpoints_PreviewEndpointsAreGatedByThePreviewFlagAlone(string route)
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationSubscriptionEndpoints();

        var preview = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.EndsWith("}/" + route));

        var feature = Assert.Single(preview.Metadata.GetOrderedMetadata<IFeatureMetadata>());
        Assert.Contains(InvoicingFeatureFlags.PM36631_PreviewDrivenCart, feature.ToString());
    }
}
