using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors</c> resource: fleet registration, enable/disable,
/// deletion, and target assignment. The Minimal API endpoints (see <c>AccessConnectorEndpoints</c>) resolve this
/// handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the
/// behavior lands with the rest of the rotation feature.
/// </remarks>
public class AccessConnectorEndpointsHandler
{
    public Task<ListResponseModel<PamAccessConnectorResponseModel>> GetAll(Guid orgId)
        => throw new NotImplementedException();

    public Task<PamAccessConnectorDetailResponseModel> Get(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task<RegisterAccessConnectorResponseModel> Post(Guid orgId, RegisterAccessConnectorRequestModel model)
        => throw new NotImplementedException();

    public Task Enable(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Disable(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Delete(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task AssignTarget(Guid orgId, Guid id, AssignAccessConnectorTargetRequestModel model)
        => throw new NotImplementedException();

    public Task UnassignTarget(Guid orgId, Guid id, Guid targetSystemId)
        => throw new NotImplementedException();
}
