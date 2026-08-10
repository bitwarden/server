using Bit.Core;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.User;

/// <summary>Maps the account-scoped subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class UserSubscriptionEndpointsExtensions
{
    /// <summary>Attaches the account subscription group and its shared cross-cutting chain. Empty at this stage; endpoints arrive with the individual screen slices.</summary>
    public static RouteGroupBuilder MapUserSubscriptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/subscriptions");
        group.WithTags("UserSubscriptions");
        group.RequireAuthorization(Policies.Application);
        group.RequireFeature(FeatureFlagKeys.PM36631_PreviewDrivenCart);
        return group;
    }
}
