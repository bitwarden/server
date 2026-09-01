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
/// Handler for the <c>organizations/{orgId}/access-connectors/rotation/target-systems</c> resource. Authority over
/// the organization is already settled by the time a handler runs -- <c>PamEndpointsExtensions</c> gates the whole
/// connector admin group on <c>ManageAccessConnectorRequirement</c> through the authorization middleware. What is
/// left is resource scoping: the commands underneath re-verify every id argument belongs to the route organization.
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
    /// The single update replaces the separate rename and policy operations, so it fans out to both commands. The
    /// automatic/manual shape rule cannot be enforced on the request model -- the body no longer carries the method
    /// that decides it -- so it is checked here against the stored method, exactly as
    /// <see cref="UpdateTargetSystemRequestModel"/> describes.
    ///
    /// The policy update goes first: it carries the stricter guards (automatic-only, and it refuses to withdraw
    /// session-termination support while a rotation config still requires it), so a rejected policy leaves the name
    /// untouched rather than half-applying the update.
    /// </remarks>
    public async Task Put(Guid orgId, Guid id, UpdateTargetSystemRequestModel model)
    {
        var hasPolicy = model.PasswordPolicy is not null && model.SupportsSessionTermination is not null;
        var hasNeither = model.PasswordPolicy is null && model.SupportsSessionTermination is null;
        if (!hasPolicy && !hasNeither)
        {
            throw new BadRequestException(
                "PasswordPolicy and SupportsSessionTermination must be sent together.");
        }

        var targetSystem = await targetSystemRepository.GetByIdAsync(id);
        if (targetSystem is null || targetSystem.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        if (targetSystem.Method == PamTargetSystemMethod.Automatic && hasNeither)
        {
            throw new BadRequestException(
                "An automatic target system requires PasswordPolicy and SupportsSessionTermination.");
        }

        if (targetSystem.Method != PamTargetSystemMethod.Automatic && hasPolicy)
        {
            throw new BadRequestException("Only automatic target systems have a password policy.");
        }

        if (hasPolicy)
        {
            await updateTargetSystemPolicyCommand.UpdateAsync(
                orgId,
                currentContext.UserId!.Value,
                id,
                model.PasswordPolicy!.ToPasswordPolicy(),
                model.SupportsSessionTermination!.Value);
        }

        await renameTargetSystemCommand.RenameAsync(orgId, currentContext.UserId!.Value, id, model.Name);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await deleteTargetSystemCommand.DeleteAsync(orgId, currentContext.UserId!.Value, id);
    }
}
