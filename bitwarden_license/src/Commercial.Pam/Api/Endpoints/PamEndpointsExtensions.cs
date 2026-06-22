using Bit.Commercial.Pam.Api.Endpoints.Filters;
using Bit.Core;
using Bit.Core.Auth.Identity;

namespace Bit.Commercial.Pam.Api.Endpoints;

/// <summary>
/// Maps the PAM HTTP surface as Minimal API endpoint groups. Each resource group shares the same cross-cutting
/// chain — authorization, exception → <c>ErrorResponseModel</c> translation, the PAM feature gate, and request-model
/// validation — reproducing what the MVC controllers received from attributes and conventions. Routes are identical
/// to the controllers they replace.
/// </summary>
public static class PamEndpointsExtensions
{
    public static void MapPamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/leases").WithPamDefaults().MapLeaseEndpoints();
        endpoints.MapGroup("/access-requests").WithPamDefaults().MapAccessRequestEndpoints();
        endpoints.MapGroup("/organizations/{orgId:guid}/access-rules").WithPamDefaults().MapAccessRuleEndpoints();
        endpoints.MapGroup("/ciphers/{id:guid}/lease").WithPamDefaults().MapCipherLeaseEndpoints();
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
