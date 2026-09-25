using System.Security.Claims;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases/ciphers/{id}</c> resource: the per-cipher leasing entry points (pre-check, state,
/// submit). The Minimal API endpoints (see <c>CipherLeaseEndpoints</c>) resolve this handler from DI. The deprecated
/// full-cipher read-back (<c>GET …/cipher</c>) is hosted separately, by a small MVC controller in the Api project,
/// since it depends on the Api Vault response models.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the PAM feature.
/// </remarks>
public class CipherLeaseEndpointsHandler
{
    public Task<AccessPreCheckResponseModel> PreCheck(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public Task<CipherAccessStateResponseModel> State(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public Task<AccessRequestResultResponseModel> Post(ClaimsPrincipal user, Guid id, AccessRequestCreateRequestModel model)
        => throw new NotImplementedException();
}
