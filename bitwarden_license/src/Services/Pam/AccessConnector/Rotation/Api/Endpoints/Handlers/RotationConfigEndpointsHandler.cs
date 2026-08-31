using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors/rotation/configs</c> resource. The Minimal API endpoints
/// (see <c>RotationConfigEndpoints</c>) resolve this handler from DI.
/// </summary>
/// <remarks>
/// Scaffold only: the method signatures define the wire contract (request/response models, status codes) that the
/// generated OpenAPI spec and client bindings are built from. The bodies are intentionally unimplemented — the behavior
/// lands with the rest of the rotation feature.
/// </remarks>
public class RotationConfigEndpointsHandler
{
    public Task<ListResponseModel<PamRotationConfigResponseModel>> GetAll(Guid orgId)
        => throw new NotImplementedException();

    public Task<PamRotationConfigDetailResponseModel> Get(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task<PamRotationConfigDetailResponseModel> Post(Guid orgId, CreateRotationConfigRequestModel model)
        => throw new NotImplementedException();

    public Task<PamRotationConfigDetailResponseModel> PutSettings(Guid orgId, Guid id, UpdateRotationSettingsRequestModel model)
        => throw new NotImplementedException();

    public Task<PamRotationConfigDetailResponseModel> PutAccount(Guid orgId, Guid id, UpdateRotationAccountRequestModel model)
        => throw new NotImplementedException();

    public Task Pause(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Resume(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Rotate(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task RecordManual(Guid orgId, Guid id)
        => throw new NotImplementedException();

    public Task Delete(Guid orgId, Guid id)
        => throw new NotImplementedException();
}
