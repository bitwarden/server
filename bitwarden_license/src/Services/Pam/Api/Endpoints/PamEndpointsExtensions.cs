using Bit.Services.Pam.Api.Endpoints.Filters;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Models.Api;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// Maps the PAM HTTP surface as Minimal API endpoint groups. Each resource group shares the same cross-cutting
/// chain — authorization, exception → <c>ErrorResponseModel</c> translation, the PAM feature gate, and request-model
/// validation.
/// </summary>
public static class PamEndpointsExtensions
{
    public static void MapPamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/organizations/{orgId:guid}/access-rules").WithPamDefaults().MapAccessRuleEndpoints();
    }

    /// <summary>
    /// Applies the shared PAM endpoint chain to a group. Order matters: the exception filter is outermost so it
    /// translates throws from the feature filter (<see cref="Bit.Core.Exceptions.FeatureUnavailableException"/>),
    /// the validation filter, and the handlers into the <c>ErrorResponseModel</c> contract.
    /// </summary>
    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.Application);
        group.AddEndpointFilter<PamExceptionHandlerEndpointFilter>();
        group.RequireFeature(FeatureFlagKeys.Pam);
        group.AddEndpointFilter<PamValidationEndpointFilter>();
        group.WithGroupName("internal");

        // Every PAM endpoint funnels thrown exceptions through PamExceptionHandlerEndpointFilter, which renders
        // them as ErrorResponseModel. Produces<T> is only available on RouteHandlerBuilder, so document the common
        // cases once for the whole group by adding the ApiExplorer metadata directly.
        group.WithMetadata(
            new ProducesResponseTypeMetadata(StatusCodes.Status400BadRequest, typeof(ErrorResponseModel), ["application/json"]),
            new ProducesResponseTypeMetadata(StatusCodes.Status404NotFound, typeof(ErrorResponseModel), ["application/json"]));
        return group;
    }

    /// <summary>
    /// Minimal API equivalent of <c>[RequireFeature(key)]</c>: gates every endpoint in the group behind the flag.
    /// </summary>
    public static RouteGroupBuilder RequireFeature(this RouteGroupBuilder group, string featureFlagKey)
    {
        group.AddEndpointFilter(new RequireFeatureEndpointFilter(featureFlagKey));
        return group;
    }
}
