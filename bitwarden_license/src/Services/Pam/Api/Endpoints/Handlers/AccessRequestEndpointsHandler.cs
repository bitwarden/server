using System.Security.Claims;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>access-requests</c> resource. The Minimal API endpoints (see <c>AccessRequestEndpoints</c>)
/// resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the PAM feature.
/// </remarks>
public class AccessRequestEndpointsHandler
{
    public Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetInbox(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetHistory(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task<ListResponseModel<AccessRequestDetailsResponseModel>> GetMine(ClaimsPrincipal user)
        => throw new NotImplementedException();

    public Task<AccessRequestDetailsResponseModel> GetDetails(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public Task<AccessRequestDetailsResponseModel> Decide(ClaimsPrincipal user, Guid id, AccessDecisionRequestModel model)
        => throw new NotImplementedException();

    public Task<AccessLeaseResponseModel> Activate(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();

    public Task Revoke(ClaimsPrincipal user, Guid id)
        => throw new NotImplementedException();
}
