using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Services.Pam.Api.Endpoints.Filters;
using Bit.Services.Pam.Rotation.Api.Endpoints;
using Bit.Services.Pam.Rotation.Api.Endpoints.Filters;

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
        endpoints.MapGroup("/organizations/{orgId:guid}/audit").WithPamDefaults().MapAuditEndpoints();
        endpoints.MapGroup("/access-requests").WithPamDefaults().MapAccessRequestEndpoints();
        endpoints.MapGroup("/organizations/{orgId:guid}/access-rules").WithPamDefaults().MapAccessRuleEndpoints();
        endpoints.MapGroup("/leases/ciphers/{id:guid}").WithPamDefaults().MapCipherLeaseEndpoints();

        // Credential rotation -- admin fleet/config management, gated behind the PamRotation flag on top of the
        // same org-scoped Policies.Application every other admin group uses.
        var rotationAdmin = endpoints.MapGroup("/organizations/{orgId:guid}/rotation").WithPamRotationDefaults();
        rotationAdmin.MapGroup("/daemons").MapRotationDaemonEndpoints();
        rotationAdmin.MapGroup("/target-systems").MapRotationTargetSystemEndpoints();
        rotationAdmin.MapGroup("/configs").MapRotationConfigEndpoints();

        // Credential rotation -- the daemon-facing surface. Policies.PamRotationDaemon replaces Policies.Application
        // (a machine-credential bearer token, not a user's), and DaemonRequestEndpointFilter re-verifies the daemon
        // end to end on every request and bumps its heartbeat.
        var rotationDaemon = endpoints.MapGroup("/rotation").WithPamDaemonDefaults();
        rotationDaemon.MapGroup("/daemon").MapRotationDaemonJobsEndpoints();
        rotationDaemon.MapGroup("/jobs").MapRotationJobEndpoints();
        rotationDaemon.MapGroup("/attempts").MapRotationAttemptEndpoints();
    }

    /// <summary>Applies the shared PAM endpoint chain with the surface's usual authorization policy and feature flag.</summary>
    private static RouteGroupBuilder WithPamDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.Pam);

    /// <summary>
    /// Rotation's admin surface: org admin/owner-gated (per-row org checks happen in the commands), behind the
    /// rotation flag rather than the base PAM flag.
    /// </summary>
    private static RouteGroupBuilder WithPamRotationDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.Application, FeatureFlagKeys.PamRotation);

    /// <summary>
    /// Rotation's daemon-facing surface: <see cref="Policies.PamRotationDaemon"/> instead of the user-token
    /// <see cref="Policies.Application"/>, plus <see cref="DaemonRequestEndpointFilter"/> on every route to
    /// re-verify the daemon and bump its heartbeat. Runs after the feature/validation filters so a disabled flag or
    /// an invalid body short-circuits before the extra daemon lookup.
    ///
    /// TODO(PM-39040): rate-limit this group by client_id.
    /// </summary>
    private static RouteGroupBuilder WithPamDaemonDefaults(this RouteGroupBuilder group) =>
        group.WithPamDefaults(Policies.PamRotationDaemon, FeatureFlagKeys.PamRotation)
            .AddEndpointFilter<DaemonRequestEndpointFilter>();

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
