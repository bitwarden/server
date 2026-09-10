using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/jobs</c> actions: the poll, which is the only
/// request an idle access connector makes, and the atomic first-claim-wins claim, which hands back the work snapshot
/// the access connector executes against. The Minimal API endpoints (see <c>RotationJobEndpoints</c>) resolve this
/// handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the behavior
/// lands with the rest of the rotation feature.
/// </remarks>
public class RotationJobEndpointsHandler
{
    public Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
        => throw new NotImplementedException();

    public Task<RotationClaimResponseModel> Claim(Guid id)
        => throw new NotImplementedException();
}
