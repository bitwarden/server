using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors/rotation/target-systems</c> resource. The Minimal API endpoints
/// (see <c>TargetSystemEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the behavior
/// lands with the rest of the rotation feature.
/// </remarks>
public class TargetSystemEndpointsHandler
{
    public Task<ListResponseModel<PamTargetSystemResponseModel>> GetAll(Guid orgId)
        => throw new NotImplementedException();

    public Task<PamTargetSystemResponseModel> Post(Guid orgId, RegisterTargetSystemRequestModel model)
        => throw new NotImplementedException();

    public Task Enable(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Disable(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Put(Guid orgId, Guid id, UpdateTargetSystemRequestModel model)
        => throw new NotImplementedException();

    public Task Delete(Guid orgId, Guid id)
        => throw new NotImplementedException();
}
