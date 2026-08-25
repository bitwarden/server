using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Api.Models.Request;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/rotation/target-systems</c> resource. Authority over the organization is
/// already settled by the time a handler runs -- <c>PamEndpointsExtensions</c> gates the whole rotation admin group on
/// <c>ManageRotationRequirement</c> through the authorization middleware. What is left is resource scoping: the
/// commands underneath re-verify every id argument belongs to the route organization.
/// </summary>
public class RotationTargetSystemEndpointsHandler(
    ICurrentContext currentContext,
    IPamTargetSystemRepository targetSystemRepository,
    IRegisterTargetSystemCommand registerTargetSystemCommand,
    ISetTargetSystemStatusCommand setTargetSystemStatusCommand,
    IRenameTargetSystemCommand renameTargetSystemCommand,
    IUpdateTargetSystemPolicyCommand updateTargetSystemPolicyCommand)
{
    public async Task<ListResponseModel<PamTargetSystemResponseModel>> GetAll(Guid orgId)
    {
        var targetSystems = await targetSystemRepository.GetManyByOrganizationIdAsync(orgId);
        return new ListResponseModel<PamTargetSystemResponseModel>(
            targetSystems.Select(targetSystem => new PamTargetSystemResponseModel(targetSystem)));
    }

    public async Task<PamTargetSystemResponseModel> Post(Guid orgId, RegisterTargetSystemRequestModel model)
    {
        var targetSystem = await registerTargetSystemCommand.RegisterAsync(
            orgId,
            currentContext.UserId!.Value,
            model.Name,
            model.Method,
            model.Kind,
            model.PasswordPolicy?.ToPasswordPolicy(),
            model.SupportsSessionTermination);
        return new PamTargetSystemResponseModel(targetSystem);
    }

    public async Task Enable(Guid orgId, Guid id)
    {
        await setTargetSystemStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: true);
    }

    public async Task Disable(Guid orgId, Guid id)
    {
        await setTargetSystemStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: false);
    }

    public async Task Rename(Guid orgId, Guid id, RenameTargetSystemRequestModel model)
    {
        await renameTargetSystemCommand.RenameAsync(orgId, currentContext.UserId!.Value, id, model.Name);
    }

    public async Task UpdatePolicy(Guid orgId, Guid id, UpdateTargetSystemPolicyRequestModel model)
    {
        await updateTargetSystemPolicyCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.PasswordPolicy.ToPasswordPolicy(), model.SupportsSessionTermination);
    }
}
