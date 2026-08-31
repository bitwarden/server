using Bit.Core.Context;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>access-connectors/rotation/attempts/{id}</c> connector-facing actions: reading and writing back
/// the claimed attempt's cipher, and reporting its outcome. Runs behind <c>Policies.PamRotationDaemon</c>; every
/// command throws 404 for an unknown attempt id (no audit -- nothing to audit against) and 409 for a stale report or
/// a lost write race (audited as <c>report_rejected</c> / <c>write_rejected</c>).
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
        var cipher = await getRotationCipherQuery.GetAsync(currentContext.PamDaemonId!.Value, id);
        return new RotationCipherResponseModel(cipher);
    }

    public async Task PutCipher(Guid id, SubmitCipherUpdateRequestModel model)
    {
        await submitCipherUpdateCommand.SubmitAsync(
            currentContext.PamDaemonId!.Value, id, model.Data, model.LastKnownRevisionDate!.Value);
    }

    public async Task Success(Guid id, ReportRotationSucceededRequestModel model)
    {
        await reportRotationSucceededCommand.ReportSucceededAsync(
            currentContext.PamDaemonId!.Value, id, model.SessionTermination!.Value);
    }

    /// <summary>
    /// The contract forbids forwarding raw target-system error output (it can echo credentials) -- the access
    /// connector sends only a bounded <see cref="ReportRotationFailedRequestModel.ErrorCode"/> plus optional
    /// <see cref="ReportRotationFailedRequestModel.Detail"/>, combined here and truncated to 500 characters by the
    /// command regardless (never rejected).
    /// </summary>
    public async Task Failure(Guid id, ReportRotationFailedRequestModel model)
    {
        await reportRotationFailedCommand.ReportFailedAsync(
            currentContext.PamDaemonId!.Value, id, model.ToFailureReason(), model.SyncState!.Value);
    }
}
