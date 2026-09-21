using Bit.Core.Auth.Identity;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.Subscriptions.User.Test;

public class UserSubscriptionEndpointsTests
{
    [Fact]
    public void MapUserSubscriptionEndpoints_AppliesAuthorizationFeatureGateAndTags()
    {
        var app = WebApplication.CreateBuilder().Build();

        var group = app.MapUserSubscriptionEndpoints();
        group.MapGet("/__probe", () => Results.Ok());

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("__probe"));

        var authorize = endpoint.Metadata.GetMetadata<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(Policies.Application, authorize!.Policy);

        // Feature gate: IFeatureMetadata (Bitwarden.Server.Sdk.Features) exposes a FeatureCheck
        // delegate, not a flag-name string — presence guards against a dropped RequireFeature.
        var feature = endpoint.Metadata.GetMetadata<IFeatureMetadata>();
        Assert.NotNull(feature);

        var tags = endpoint.Metadata.GetMetadata<ITagsMetadata>();
        Assert.NotNull(tags);
        Assert.Contains("UserSubscriptions", tags!.Tags);

        // Group name "internal" keeps these endpoints out of the published Public API spec
        // (api.public.json); MVC controllers get this from ApiExplorerGroupConvention, but
        // Minimal API groups must set it explicitly.
        var groupName = endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>();
        Assert.NotNull(groupName);
        Assert.Equal("internal", groupName!.EndpointGroupName);
    }
}
