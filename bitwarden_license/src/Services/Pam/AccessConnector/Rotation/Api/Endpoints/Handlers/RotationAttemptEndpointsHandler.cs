using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>access-connectors/rotation/attempts/{id}</c> connector-facing actions: reading and writing back
/// the claimed attempt's cipher, and reporting its outcome. The Minimal API endpoints (see
/// <c>RotationAttemptEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the behavior
/// lands with the rest of the rotation feature.
/// </remarks>
public class RotationAttemptEndpointsHandler
{
    public Task<RotationCipherResponseModel> GetCipher(Guid id)
        => throw new NotImplementedException();

    public Task PutCipher(Guid id, SubmitCipherUpdateRequestModel model)
        => throw new NotImplementedException();

    public Task Success(Guid id, ReportRotationSucceededRequestModel model)
        => throw new NotImplementedException();

    public Task Failure(Guid id, ReportRotationFailedRequestModel model)
        => throw new NotImplementedException();
}
