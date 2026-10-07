using Bit.Core.Context;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/attempts/{id}</c> actions. The commands throw 404
/// for an unknown attempt (not audited) and 409 for a stale report or a lost write race (audited).
/// </summary>
public class RotationAttemptEndpointsHandler(
    ICurrentContext currentContext,
    IGetRotationCipherQuery getRotationCipherQuery,
    ISubmitCipherUpdateCommand submitCipherUpdateCommand,
    IReportRotationSucceededCommand reportRotationSucceededCommand,
    IReportRotationFailedCommand reportRotationFailedCommand)
{
    public async Task<RotationCipherResponseModel> GetCipher(Guid id)
    {
        var cipher = await getRotationCipherQuery.GetAsync(currentContext.PamAccessConnectorId!.Value, id);
        return new RotationCipherResponseModel(cipher);
    }

    public async Task PutCipher(Guid id, SubmitCipherUpdateRequestModel model)
    {
        await submitCipherUpdateCommand.SubmitAsync(
            currentContext.PamAccessConnectorId!.Value, id, model.Data, model.LastKnownRevisionDate!.Value);
    }

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
