using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Pam.Enums;
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
    ICurrentContext currentContext,
    IPamTargetSystemRepository targetSystemRepository,
    IRegisterTargetSystemCommand registerTargetSystemCommand,
    ISetTargetSystemStatusCommand setTargetSystemStatusCommand,
    IRenameTargetSystemCommand renameTargetSystemCommand,
    IUpdateTargetSystemPolicyCommand updateTargetSystemPolicyCommand,
    IDeleteTargetSystemCommand deleteTargetSystemCommand)
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
            model.Method!.Value,
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

    /// <remarks>
    /// Fans out to the policy and rename commands, policy first since its guards are stricter, so a rejected policy
    /// leaves the name untouched.
    /// </remarks>
    public async Task Put(Guid orgId, Guid id, UpdateTargetSystemRequestModel model)
    {
        var targetSystem = await targetSystemRepository.GetByIdAsync(id);
        if (targetSystem is null || targetSystem.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        var isAutomatic = targetSystem.Method == PamTargetSystemMethod.Automatic;
        if (isAutomatic)
        {
            if (model.PasswordPolicy is null || model.SupportsSessionTermination is null)
            {
                throw new BadRequestException(
                    "An automatic target system requires PasswordPolicy and SupportsSessionTermination.");
            }
        }
        else if (model.SupportsSessionTermination is true)
        {
            throw new BadRequestException("A manual target system cannot support session termination.");
        }

        if (model.PasswordPolicy is not null)
        {
            await updateTargetSystemPolicyCommand.UpdateAsync(
                orgId,
                currentContext.UserId!.Value,
                id,
                model.PasswordPolicy.ToPasswordPolicy(),
                isAutomatic ? model.SupportsSessionTermination!.Value : null);
        }

        await renameTargetSystemCommand.RenameAsync(orgId, currentContext.UserId!.Value, id, model.Name);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await deleteTargetSystemCommand.DeleteAsync(orgId, currentContext.UserId!.Value, id);
    }
}
