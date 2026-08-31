using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Api.Models.Response;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors</c> resource: fleet registration, enable/disable,
/// deletion, and target assignment. Authority over the organization is already settled by the time a handler runs --
/// <c>PamEndpointsExtensions</c> gates the whole connector admin group on <c>ManageAccessConnectorRequirement</c>
/// through the authorization middleware. What is left is resource scoping: the commands underneath re-verify every
/// id argument belongs to the route organization (404, never 403 -- no existence oracle over comb GUIDs).
/// </summary>
public class AccessConnectorEndpointsHandler(
    ICurrentContext currentContext,
    IListAccessConnectorsQuery listAccessConnectorsQuery,
    IGetAccessConnectorDetailsQuery getAccessConnectorDetailsQuery,
    IRegisterAccessConnectorCommand registerAccessConnectorCommand,
    ISetAccessConnectorStatusCommand setAccessConnectorStatusCommand,
    IDeleteAccessConnectorCommand deleteAccessConnectorCommand,
    IAssignAccessConnectorToTargetCommand assignAccessConnectorToTargetCommand,
    IUnassignAccessConnectorFromTargetCommand unassignAccessConnectorFromTargetCommand)
{
    public async Task<ListResponseModel<PamAccessConnectorResponseModel>> GetAll(Guid orgId)
    {
        var connectors = await listAccessConnectorsQuery.ListAsync(orgId);
        return new ListResponseModel<PamAccessConnectorResponseModel>(
            connectors.Select(connector => new PamAccessConnectorResponseModel(connector)));
    }

    public async Task<PamAccessConnectorDetailResponseModel> Get(Guid orgId, Guid id)
    {
        var history = await getAccessConnectorDetailsQuery.GetAsync(orgId, id);
        return new PamAccessConnectorDetailResponseModel(history);
    }

    public async Task<RegisterAccessConnectorResponseModel> Post(Guid orgId, RegisterAccessConnectorRequestModel model)
    {
        var result = await registerAccessConnectorCommand.RegisterAsync(
            orgId, currentContext.UserId!.Value, model.Name, model.EncryptedPayload, model.Key);
        return new RegisterAccessConnectorResponseModel(result);
    }

    public async Task Enable(Guid orgId, Guid id)
    {
        await setAccessConnectorStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: true);
    }

    public async Task Disable(Guid orgId, Guid id)
    {
        await setAccessConnectorStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: false);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await deleteAccessConnectorCommand.DeleteAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task AssignTarget(Guid orgId, Guid id, AssignAccessConnectorTargetRequestModel model)
    {
        await assignAccessConnectorToTargetCommand.AssignAsync(
            orgId, currentContext.UserId!.Value, id, model.TargetSystemId);
    }

    public async Task UnassignTarget(Guid orgId, Guid id, Guid targetSystemId)
    {
        await unassignAccessConnectorFromTargetCommand.UnassignAsync(
            orgId, currentContext.UserId!.Value, id, targetSystemId);
    }
}
