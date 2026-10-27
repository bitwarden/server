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
using NSubstitute;
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

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        Assert.True(feature!.FeatureCheck(featureService));
        featureService.Received().IsEnabled(InvoicingFeatureFlags.PM36631_PreviewDrivenCart, Arg.Any<bool>());
        featureService.DidNotReceive().IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support, Arg.Any<bool>());

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
}
