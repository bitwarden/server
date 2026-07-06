using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Services.Pam.Rotation.Api.Models.Request;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/rotation/target-systems</c> resource. Every method is org admin/owner-gated
/// (see <see cref="EnsureAdminAsync"/>, copied from <c>AccessRuleEndpointsHandler</c>); the commands underneath
/// additionally re-verify every id argument belongs to the route organization.
/// </summary>
public class RotationTargetSystemEndpointsHandler(
    ICurrentContext currentContext,
    IListTargetSystemsQuery listTargetSystemsQuery,
    IRegisterTargetSystemCommand registerTargetSystemCommand,
    ISetTargetSystemStatusCommand setTargetSystemStatusCommand,
    IRenameTargetSystemCommand renameTargetSystemCommand,
    IUpdateTargetSystemPolicyCommand updateTargetSystemPolicyCommand)
{
    public async Task<ListResponseModel<PamTargetSystemResponseModel>> GetAll(Guid orgId)
    {
        await EnsureAdminAsync(orgId);

        var targetSystems = await listTargetSystemsQuery.ListAsync(orgId);
        return new ListResponseModel<PamTargetSystemResponseModel>(
            targetSystems.Select(targetSystem => new PamTargetSystemResponseModel(targetSystem)));
    }

    public async Task<PamTargetSystemResponseModel> Post(Guid orgId, RegisterTargetSystemRequestModel model)
    {
        await EnsureAdminAsync(orgId);

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
        await EnsureAdminAsync(orgId);

        await setTargetSystemStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: true);
    }

    public async Task Disable(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await setTargetSystemStatusCommand.SetStatusAsync(orgId, currentContext.UserId!.Value, id, enable: false);
    }

    public async Task Rename(Guid orgId, Guid id, RenameTargetSystemRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        await renameTargetSystemCommand.RenameAsync(orgId, currentContext.UserId!.Value, id, model.Name);
    }

    public async Task UpdatePolicy(Guid orgId, Guid id, UpdateTargetSystemPolicyRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        await updateTargetSystemPolicyCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.PasswordPolicy.ToPasswordPolicy(), model.SupportsSessionTermination);
    }

    private async Task EnsureAdminAsync(Guid orgId)
    {
        if (!await currentContext.OrganizationAdmin(orgId) && !await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }
    }
}
