using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;

/// <summary>
/// Records the daemon's heartbeat on every daemon-facing rotation route (spec <c>DaemonConnection</c>) by bumping
/// <see cref="PamDaemon.LastHeartbeatAt"/>. Every daemon request counts as a sign of life -- the poll most of all,
/// which is all an idle daemon ever sends -- so the write belongs here rather than in any one handler. The write is
/// conditional in the repository, which only touches the row once per <c>HeartbeatMinInterval</c>, so a tightly
/// polling daemon does not hammer it.
/// </summary>
/// <remarks>
/// This filter authorizes nothing. A daemon's eligibility is established at token issuance and re-established by
/// the work queries themselves:
/// <list type="bullet">
/// <item><see cref="Bit.Core.Auth.Identity.Policies.PamRotationDaemon"/> proves the caller holds a
/// RotationDaemon-scoped token.</item>
/// <item><c>PamDaemonClientProvider</c> issues that token only to an Enabled daemon whose organization is enabled
/// and licensed for PAM, and caps its lifetime so a stale token is short-lived.</item>
/// <item><c>PamRotationJob_ReadManyClaimableByDaemonId</c> and <c>PamRotationJob_Claim</c> join <c>PamDaemon</c> on
/// Enabled and on the config's organization, so a daemon disabled or deleted mid-token sees no work and can claim
/// none.</item>
/// </list>
/// </remarks>
public class AccessConnectorHeartbeatEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Endpoint filters registered via the generic AddEndpointFilter<T>() are resolved per invocation from the
        // request's scoped provider (mirrors RequireFeatureEndpointFilter's IFeatureService lookup) rather than
        // constructor-injected, since a filter instance can otherwise be built once and outlive any single request.
        var services = context.HttpContext.RequestServices;
        var currentContext = services.GetRequiredService<ICurrentContext>();
        var daemonRepository = services.GetRequiredService<IPamDaemonRepository>();
        var options = services.GetRequiredService<IOptions<PamRotationOptions>>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        var daemonId = currentContext.PamDaemonId ?? throw new NotFoundException();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await daemonRepository.UpdateHeartbeatAsync(daemonId, now, options.Value.HeartbeatMinInterval);

        return await next(context);
    }
}
