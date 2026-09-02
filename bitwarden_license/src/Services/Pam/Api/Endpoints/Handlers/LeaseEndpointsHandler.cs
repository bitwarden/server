using System.Security.Claims;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>leases</c> resource. The Minimal API endpoints (see <c>LeaseEndpoints</c>) resolve this
/// handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the PAM feature.
/// </remarks>
public class LeaseEndpointsHandler
{
    public Task<ListResponseModel<AccessLeaseResponseModel>> GetActive(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task<ListResponseModel<AccessLeaseResponseModel>> GetHistory(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task<ListResponseModel<AccessLeaseResponseModel>> GetMine(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task Revoke(ClaimsPrincipal user, Guid id, AccessLeaseRevokeRequestModel model)
        => throw new NotImplementedException();

    public Task<AccessRequestDetailsResponseModel> Extend(ClaimsPrincipal user, Guid id, AccessLeaseExtensionRequestModel model)
        => throw new NotImplementedException();
}
