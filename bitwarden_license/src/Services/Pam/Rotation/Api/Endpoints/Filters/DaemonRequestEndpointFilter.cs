using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Filters;

/// <summary>
/// Runs on every daemon-facing rotation route, after <see cref="Bit.Core.Auth.Identity.Policies.PamRotationDaemon"/>
/// authorization has already established the caller as a RotationDaemon-scoped bearer token. A bearer JWT can
/// outlive a revocation or a license lapse by up to its lifetime, so this filter re-verifies the daemon end to end on
/// every request: the token's <c>sub</c> claim (<see cref="ICurrentContext.PamDaemonId"/>) must still name a
/// <see cref="PamDaemonStatus.Enrolled"/> daemon whose organization is both licensed for PAM and not suspended.
/// Every rejection is a <see cref="NotFoundException"/> (404) -- never a distinct error -- so a revoked daemon or a
/// disabled organization cannot be distinguished from an unknown one.
///
/// On success this also doubles as the daemon's heartbeat (spec <c>DaemonConnection</c>): it conditionally bumps
/// <see cref="PamDaemon.LastHeartbeatAt"/> (the repository only writes when the existing value is stale, so a
/// tightly polling daemon does not hammer its row) and stashes the loaded daemon on
/// <see cref="HttpContext.Items"/> under <see cref="PamDaemonHttpContextKey"/> so handlers avoid a second lookup.
/// </summary>
public class DaemonRequestEndpointFilter : IEndpointFilter
{
    /// <summary>The <see cref="HttpContext.Items"/> key handlers read to get the <see cref="PamDaemon"/> this filter already loaded.</summary>
    public const string PamDaemonHttpContextKey = "PamDaemon";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Endpoint filters registered via the generic AddEndpointFilter<T>() are resolved per invocation from the
        // request's scoped provider (mirrors RequireFeatureEndpointFilter's IFeatureService lookup) rather than
        // constructor-injected, since a filter instance can otherwise be built once and outlive any single request.
        var services = context.HttpContext.RequestServices;
        var currentContext = services.GetRequiredService<ICurrentContext>();
        var daemonRepository = services.GetRequiredService<IPamDaemonRepository>();
        var organizationAbilityCacheService = services.GetRequiredService<IOrganizationAbilityCacheService>();
        var options = services.GetRequiredService<IOptions<PamRotationOptions>>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        var daemonId = currentContext.PamDaemonId ?? throw new NotFoundException();

        var daemon = await daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.Status != PamDaemonStatus.Enrolled)
        {
            throw new NotFoundException();
        }

        // Closes the revocation/license-lapse latency window a short-lived JWT + client token cache would otherwise
        // leave open: the daemon's organization must currently be enabled and licensed for PAM, not merely at the
        // time the token was issued.
        var organizationAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(daemon.OrganizationId);
        if (organizationAbility is not { Enabled: true, UsePam: true })
        {
            throw new NotFoundException();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await daemonRepository.UpdateHeartbeatAsync(daemon.Id, now, options.Value.HeartbeatMinInterval);

        context.HttpContext.Items[PamDaemonHttpContextKey] = daemon;

        return await next(context);
    }
}
