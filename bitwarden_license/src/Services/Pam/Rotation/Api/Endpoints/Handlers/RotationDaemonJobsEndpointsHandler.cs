using Bit.HttpExtensions;
using Bit.Services.Pam.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>GET rotation/daemon/jobs</c> -- the daemon's poll, and the only request an idle daemon makes.
/// The Minimal API endpoints (see <c>RotationDaemonJobsEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the rotation feature.
/// </remarks>
public class RotationDaemonJobsEndpointsHandler
{
    public Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
        => throw new NotImplementedException();
}
