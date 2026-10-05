using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;

/// <summary>
/// Records the access connector's heartbeat on every connector-facing rotation route by bumping
/// <see cref="PamAccessConnector.LastHeartbeatAt"/>. The write is conditional in the repository, touching the row at
/// most once per <c>HeartbeatMinInterval</c>, so a tightly polling access connector does not hammer it.
/// </summary>
/// <remarks>
/// This filter authorizes nothing. An access connector's eligibility is established at token issuance
/// (<see cref="Bit.Core.Auth.Identity.Policies.PamAccessConnector"/>, <c>PamAccessConnectorClientProvider</c>) and
/// re-established by the work queries themselves, which join <c>PamAccessConnector</c> on Enabled and organization.
/// </remarks>
public class AccessConnectorHeartbeatEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Resolved per invocation from the scoped provider rather than constructor-injected, since a filter
        // instance registered via AddEndpointFilter<T>() can otherwise be built once and outlive any one request.
        var services = context.HttpContext.RequestServices;
        var currentContext = services.GetRequiredService<ICurrentContext>();
        var accessConnectorRepository = services.GetRequiredService<IPamAccessConnectorRepository>();
        var options = services.GetRequiredService<IOptions<PamRotationOptions>>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        var accessConnectorId = currentContext.PamAccessConnectorId ?? throw new NotFoundException();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await accessConnectorRepository.UpdateHeartbeatAsync(
            accessConnectorId, now, options.Value.HeartbeatMinInterval);

        return await next(context);
    }
}
