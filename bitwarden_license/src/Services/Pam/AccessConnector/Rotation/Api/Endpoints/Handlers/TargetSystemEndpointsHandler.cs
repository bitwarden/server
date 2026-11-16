using System.Security.Claims;
using Bit.Core.Services;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors/rotation/target-systems</c> resource, authorized in the
/// middleware by <c>ManageAccessConnectorRequirement</c>.
/// </summary>
public class TargetSystemEndpointsHandler(
    IUserService userService,
    IPamTargetSystemRepository targetSystemRepository,
    IRegisterTargetSystemCommand registerTargetSystemCommand,
    ISetTargetSystemStatusCommand setTargetSystemStatusCommand,
    IUpdateTargetSystemCommand updateTargetSystemCommand,
    IDeleteTargetSystemCommand deleteTargetSystemCommand)
{
    public async Task<ListResponseModel<PamTargetSystemResponseModel>> GetAll(Guid orgId)
    {
        var targetSystems = await targetSystemRepository.GetManyByOrganizationIdAsync(orgId);
        return new ListResponseModel<PamTargetSystemResponseModel>(
            targetSystems.Select(targetSystem => new PamTargetSystemResponseModel(targetSystem)));
    }

    public async Task<PamTargetSystemResponseModel> Post(
        ClaimsPrincipal user, Guid orgId, RegisterTargetSystemRequestModel model)
    {
        var targetSystem = await registerTargetSystemCommand.RegisterAsync(
            orgId,
            userService.GetProperUserId(user)!.Value,
            model.Name,
            model.Method!.Value);
        return new PamTargetSystemResponseModel(targetSystem);
    }

    public async Task Enable(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await setTargetSystemStatusCommand.SetStatusAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, enable: true);
    }

    public async Task Disable(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await setTargetSystemStatusCommand.SetStatusAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, enable: false);
    }

    public async Task Put(ClaimsPrincipal user, Guid orgId, Guid id, UpdateTargetSystemRequestModel model)
    {
        await updateTargetSystemCommand.UpdateAsync(
            orgId, userService.GetProperUserId(user)!.Value, id, model.Name, model.SealedData, model.WrappedKey);
    }

    public async Task Delete(ClaimsPrincipal user, Guid orgId, Guid id)
    {
        await deleteTargetSystemCommand.DeleteAsync(orgId, userService.GetProperUserId(user)!.Value, id);
    }
}
