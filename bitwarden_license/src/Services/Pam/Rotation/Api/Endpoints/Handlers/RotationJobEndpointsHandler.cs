using Bit.Services.Pam.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>POST rotation/jobs/{id}/claim</c> -- the atomic first-claim-wins claim, which hands back the work
/// snapshot the daemon executes against. The Minimal API endpoints (see <c>RotationJobEndpoints</c>) resolve this
/// handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the rotation feature.
/// </remarks>
public class RotationJobEndpointsHandler
{
    public Task<RotationClaimResponseModel> Claim(Guid id)
        => throw new NotImplementedException();
}
