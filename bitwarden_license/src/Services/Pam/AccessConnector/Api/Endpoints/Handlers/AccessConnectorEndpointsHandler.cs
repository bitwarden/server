using System.Security.Claims;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Api.Models.Response;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors</c> resource, authorized in the middleware by
/// <c>ManageAccessConnectorRequirement</c>. The commands check the plan and edit access.
/// </summary>
public class AccessConnectorEndpointsHandler(
    IUserService userService,
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

    public async Task<RegisterAccessConnectorResponseModel> Post(
        ClaimsPrincipal user, Guid orgId, RegisterAccessConnectorRequestModel model)
    {
        var result = await registerAccessConnectorCommand.RegisterAsync(
            orgId, userService.GetProperUserId(user)!.Value, model.Name, model.EncryptedPayload, model.Key);
        return new RegisterAccessConnectorResponseModel(result);
    }

    public async Task Enable(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await setAccessConnectorStatusCommand.SetStatusAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, enable: true);
    }

    public async Task Disable(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await setAccessConnectorStatusCommand.SetStatusAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, enable: false);
    }

    public async Task Delete(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await deleteAccessConnectorCommand.DeleteAsync(orgId, userService.GetProperUserId(user)!.Value, id);
    }

    public async Task AssignTarget(
        ClaimsPrincipal user, Guid orgId, Guid id, AssignAccessConnectorTargetRequestModel model)
    {
        await assignAccessConnectorToTargetCommand.AssignAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, model.TargetSystemId);
    }

    public async Task UnassignTarget(ClaimsPrincipal user, Guid orgId, Guid id, Guid targetSystemId)
    {
        await unassignAccessConnectorFromTargetCommand.UnassignAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, targetSystemId);
    }
}
