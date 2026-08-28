using Bit.Api.AdminConsole.Authorization;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Services.Pam.Api.Endpoints.Filters;
using Bit.Services.Pam.Rotation.Api.Authorization;
using Bit.Services.Pam.Rotation.Api.Endpoints;

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
        endpoints.MapGroup("/leases").WithPamDefaults().MapLeaseEndpoints();
        endpoints.MapGroup("/access-requests").WithPamDefaults().MapAccessRequestEndpoints();
        endpoints.MapGroup("/organizations/{orgId:guid}/access-rules").WithPamDefaults().MapAccessRuleEndpoints();
        endpoints.MapGroup("/leases/ciphers/{id:guid}").WithPamDefaults().MapCipherLeaseEndpoints();

        // Credential rotation -- admin fleet/config management.
        var rotationAdmin = endpoints.MapGroup("/organizations/{orgId:guid}/rotation").WithPamRotationDefaults();
        rotationAdmin.MapGroup("/daemons").MapRotationDaemonEndpoints();
        rotationAdmin.MapGroup("/target-systems").MapRotationTargetSystemEndpoints();
        rotationAdmin.MapGroup("/configs").MapRotationConfigEndpoints();

        // Credential rotation -- the daemon-facing surface, reached by a machine credential rather than a user's.
        var rotationDaemon = endpoints.MapGroup("/rotation").WithPamDaemonDefaults();
        rotationDaemon.MapGroup("/daemon").MapRotationDaemonJobsEndpoints();
        rotationDaemon.MapGroup("/jobs").MapRotationJobEndpoints();
        rotationDaemon.MapGroup("/attempts").MapRotationAttemptEndpoints();
    }

    /// <summary>Applies the shared PAM endpoint chain with the surface's usual authorization policy and feature flag.</summary>
    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.Pam);

    /// <summary>
    /// Rotation's admin surface: behind the rotation flag rather than the base PAM flag, and authorized in the
    /// middleware by <see cref="ManageRotationRequirement"/> rather than in the handlers. Handlers and commands are
    /// left with resource scoping only -- confirming an id reached by route belongs to the route organization.
    /// </summary>
    private static RouteGroupBuilder WithPamRotationDefaults(this RouteGroupBuilder group)
    {
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.PamRotation);
        group.RequireAuthorization(new AuthorizeAttribute<ManageRotationRequirement>());
        return group;
    }

    /// <summary>
    /// Rotation's daemon-facing surface. These routes carry no {orgId} and no organization requirement: a daemon's
    /// organization comes from its token, and the work queries scope every read and write to it.
    ///
    /// TODO(PM-39040): these routes belong behind the machine-credential policy, not the user-token
    /// <see cref="Policies.Application"/>. That policy — and the client type and API scope it asserts on — lands
    /// with the daemon identity wiring, which must merge before the handlers below stop throwing.
    /// </summary>
    private static RouteGroupBuilder WithPamDaemonDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.PamRotation);

    /// <summary>
    /// Applies the shared PAM endpoint chain to a group for the given authorization policy and feature flag. Order
    /// matters: the exception filter is outermost so it translates throws from the feature filter, the validation
    /// filter, and the handlers into the <c>ErrorResponseModel</c> contract. The zero-argument
    /// <see cref="WithPamDefaults(RouteGroupBuilder)"/> overload delegates here with the original policy/flag, so
    /// every pre-existing group is unaffected.
    /// </summary>
    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group, string policy, string featureFlagKey)
    {
        group.RequireAuthorization(policy);
        group.WithBasicExceptionHandling();
        group.RequireFeature(featureFlagKey);
        group.AddEndpointFilter<PamValidationEndpointFilter>();
        group.WithGroupName("internal");
        return group;
    }
}
