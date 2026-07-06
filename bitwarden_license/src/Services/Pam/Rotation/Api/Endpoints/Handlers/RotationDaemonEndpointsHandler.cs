using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Services.Pam.Rotation.Api.Models.Request;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/rotation/daemons</c> resource: fleet registration, revocation, and
/// target assignment. Every method is org admin/owner-gated (see <see cref="EnsureAdminAsync"/>, copied from
/// <c>AccessRuleEndpointsHandler</c>); the commands underneath additionally re-verify every id argument belongs to
/// the route organization (404, never 403 -- no existence oracle over comb GUIDs).
/// </summary>
public class RotationDaemonEndpointsHandler(
    ICurrentContext currentContext,
    IListDaemonsQuery listDaemonsQuery,
    IRegisterDaemonCommand registerDaemonCommand,
    IRevokeDaemonCommand revokeDaemonCommand,
    IAssignDaemonToTargetCommand assignDaemonToTargetCommand,
    IUnassignDaemonFromTargetCommand unassignDaemonFromTargetCommand)
{
    public async Task<ListResponseModel<PamDaemonResponseModel>> GetAll(Guid orgId)
    {
        await EnsureAdminAsync(orgId);

        var daemons = await listDaemonsQuery.ListAsync(orgId);
        return new ListResponseModel<PamDaemonResponseModel>(
            daemons.Select(daemon => new PamDaemonResponseModel(daemon)));
    }

    public async Task<RegisterDaemonResponseModel> Post(Guid orgId, RegisterDaemonRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var result = await registerDaemonCommand.RegisterAsync(
            orgId, currentContext.UserId!.Value, model.Name, model.EncryptedPayload, model.Key);
        return new RegisterDaemonResponseModel(result);
    }

    public async Task Revoke(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await revokeDaemonCommand.RevokeAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task AssignTarget(Guid orgId, Guid id, AssignDaemonTargetRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        await assignDaemonToTargetCommand.AssignAsync(orgId, currentContext.UserId!.Value, id, model.TargetSystemId);
    }

    public async Task UnassignTarget(Guid orgId, Guid id, Guid targetSystemId)
    {
        await EnsureAdminAsync(orgId);

        await unassignDaemonFromTargetCommand.UnassignAsync(orgId, currentContext.UserId!.Value, id, targetSystemId);
    }

    private async Task EnsureAdminAsync(Guid orgId)
    {
        if (!await currentContext.OrganizationAdmin(orgId) && !await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }
    }
}
