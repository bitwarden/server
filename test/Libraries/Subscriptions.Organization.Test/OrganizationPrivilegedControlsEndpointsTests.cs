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

public class OrganizationPrivilegedControlsEndpointsTests
{
    [Fact]
    public void MapOrganizationPrivilegedControlsEndpoints_AppliesAuthorizationTagsAndGroupName()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationPrivilegedControlsEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = FindEndpoint(app, e => e.RoutePattern.RawText!.Contains("__probe"));

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute.Policy == Policies.Application);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains("OrganizationSubscriptions", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()!.EndpointGroupName);
    }

    [Fact]
    public void MapOrganizationPrivilegedControlsEndpoints_GatesOnPamBillingFlagAndNotPreviewDrivenCart()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationPrivilegedControlsEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = FindEndpoint(app, e => e.RoutePattern.RawText!.Contains("__probe"));

        var featureChecks = endpoint.Metadata.GetOrderedMetadata<IFeatureMetadata>();
        Assert.NotEmpty(featureChecks);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        Assert.All(featureChecks, metadata => Assert.True(metadata.FeatureCheck(featureService)));

        featureService.Received().IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support, Arg.Any<bool>());
        featureService.DidNotReceive().IsEnabled(InvoicingFeatureFlags.PM36631_PreviewDrivenCart, Arg.Any<bool>());
    }

    [Fact]
    public void MapOrganizationPrivilegedControlsEndpoints_PamBillingFlagOff_FeatureCheckFails()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapGroup("/{organizationId:guid}")
            .MapOrganizationPrivilegedControlsEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = FindEndpoint(app, e => e.RoutePattern.RawText!.Contains("__probe"));

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
        featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support, Arg.Any<bool>()).Returns(false);

        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IFeatureMetadata>(),
            metadata => !metadata.FeatureCheck(featureService));
    }

    [Fact]
    public void MapOrganizationPrivilegedControlsEndpoints_MapsPutChangeSeatsForStandaloneOrganizationOwners()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGroup("/{organizationId:guid}").MapOrganizationPrivilegedControlsEndpoints();

        var endpoint = FindEndpoint(app,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "ChangePrivilegedControlsSeats");

        Assert.Equal(["PUT"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.EndsWith("/privileged-controls", endpoint.RoutePattern.RawText);

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);
    }

    [Fact]
    public void MapOrganizationPrivilegedControlsEndpoints_MapsDeleteRemoveForStandaloneOrganizationOwners()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGroup("/{organizationId:guid}").MapOrganizationPrivilegedControlsEndpoints();

        var endpoint = FindEndpoint(app,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "RemovePrivilegedControls");

        Assert.Equal(["DELETE"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
        Assert.EndsWith("/privileged-controls", endpoint.RoutePattern.RawText);

        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<OrganizationBillingRequirement>);
        Assert.Contains(authorizeAttributes, attribute => attribute is AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>);
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, Func<RouteEndpoint, bool> predicate) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(predicate);
}
