using Bit.Commercial.Pam.Api.Models.Request;
using Bit.Commercial.Pam.Api.Models.Response;
using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-rules</c> resource. The Minimal API endpoints (see
/// <c>AccessRuleEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the PAM feature.
/// </remarks>
public class AccessRuleEndpointsHandler
{
    public Task<ListResponseModel<AccessRuleResponseModel>> GetAll(Guid orgId)
        => throw new NotImplementedException();

    public Task<AccessRuleResponseModel> Get(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task<AccessRuleResponseModel> Post(Guid orgId, AccessRuleRequestModel model)
        => throw new NotImplementedException();

    public Task<AccessRuleResponseModel> Put(Guid orgId, Guid id, AccessRuleRequestModel model)
        => throw new NotImplementedException();

    public Task Delete(Guid orgId, Guid id)
        => throw new NotImplementedException();
}
