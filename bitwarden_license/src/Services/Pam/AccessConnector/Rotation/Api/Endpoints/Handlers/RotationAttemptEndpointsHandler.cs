using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/attempts/{id}</c> report actions. The Minimal API
/// endpoints (see <c>RotationAttemptEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the behavior
/// lands with the rest of the rotation feature.
/// </remarks>
public class RotationAttemptEndpointsHandler
{
    public Task Success(Guid id, ReportRotationSucceededRequestModel model)
        => throw new NotImplementedException();

    public Task Failure(Guid id, ReportRotationFailedRequestModel model)
        => throw new NotImplementedException();
}
