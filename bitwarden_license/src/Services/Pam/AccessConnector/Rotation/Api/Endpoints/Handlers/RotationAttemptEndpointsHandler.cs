using Bit.Core.Context;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/attempts/{id}</c> report actions. The commands throw
/// 404 for an unknown attempt (not audited) and 409 for a stale report (audited).
/// </summary>
public class RotationAttemptEndpointsHandler(
    ICurrentContext currentContext,
    IReportRotationSucceededCommand reportRotationSucceededCommand,
    IReportRotationFailedCommand reportRotationFailedCommand)
{
    public async Task Success(Guid id, ReportRotationSucceededRequestModel model)
    {
        await reportRotationSucceededCommand.ReportSucceededAsync(
            currentContext.PamAccessConnectorId!.Value, id, model.SessionTermination!.Value);
    }

    public async Task Failure(Guid id, ReportRotationFailedRequestModel model)
    {
        await reportRotationFailedCommand.ReportFailedAsync(
            currentContext.PamAccessConnectorId!.Value, id, model.ToFailureReason(), model.SyncState!.Value);
    }
}
