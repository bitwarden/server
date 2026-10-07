using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;

/// <summary>
/// Bumps <see cref="PamAccessConnector.LastHeartbeatAt"/> on every connector-facing route, at most once per
/// <c>HeartbeatMinInterval</c> since the repository write is conditional. Authorizes nothing; token issuance and the
/// work queries check eligibility.
/// </summary>
public class AccessConnectorHeartbeatEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
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
