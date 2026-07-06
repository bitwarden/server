using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Pam;
using Bit.Services.Pam.Rotation.Api.Models.Request;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/rotation/configs</c> resource. Every method is org admin/owner-gated
/// (see <see cref="EnsureAdminAsync"/>, copied from <c>AccessRuleEndpointsHandler</c>); the commands underneath
/// additionally re-verify every id argument belongs to the route organization.
///
/// <see cref="ICreateRotationConfigCommand"/> and <see cref="IUpdateRotationSettingsCommand"/>/
/// <see cref="IUpdateRotationAccountCommand"/> return the bare entity, not the list/detail projection, so this
/// handler re-reads through <see cref="IGetRotationConfigDetailsQuery"/> after a write to respond with the same
/// shape <c>GET configs/{id}</c> uses -- one extra round trip on writes, in exchange for a single enrichment path.
/// </summary>
public class RotationConfigEndpointsHandler(
    ICurrentContext currentContext,
    TimeProvider timeProvider,
    IListRotationConfigsQuery listRotationConfigsQuery,
    IGetRotationConfigDetailsQuery getRotationConfigDetailsQuery,
    ICreateRotationConfigCommand createRotationConfigCommand,
    IUpdateRotationSettingsCommand updateRotationSettingsCommand,
    IUpdateRotationAccountCommand updateRotationAccountCommand,
    IPauseRotationCommand pauseRotationCommand,
    IResumeRotationCommand resumeRotationCommand,
    ITriggerRotationCommand triggerRotationCommand,
    IRecordManualRotationCommand recordManualRotationCommand,
    IDeleteRotationConfigCommand deleteRotationConfigCommand)
{
    public async Task<ListResponseModel<PamRotationConfigResponseModel>> GetAll(Guid orgId)
    {
        await EnsureAdminAsync(orgId);

        var configs = await listRotationConfigsQuery.ListAsync(orgId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new ListResponseModel<PamRotationConfigResponseModel>(
            configs.Select(config => new PamRotationConfigResponseModel(
                config, PamRotationRules.AwaitingManualRotation(config, config.TargetSystemMethod, now))));
    }

    public async Task<PamRotationConfigDetailResponseModel> Get(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        return await GetDetailAsync(orgId, id);
    }

    public async Task<PamRotationConfigDetailResponseModel> Post(Guid orgId, CreateRotationConfigRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var created = await createRotationConfigCommand.CreateAsync(
            orgId,
            currentContext.UserId!.Value,
            model.CipherId,
            model.TargetSystemId,
            model.AccountIdentity,
            model.TerminateSessions,
            model.ScheduleCron,
            model.RotateOnAccessEnd);
        return await GetDetailAsync(orgId, created.Id);
    }

    public async Task<PamRotationConfigDetailResponseModel> PutSettings(Guid orgId, Guid id, UpdateRotationSettingsRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var updated = await updateRotationSettingsCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.ScheduleCron, model.RotateOnAccessEnd);
        return await GetDetailAsync(orgId, updated.Id);
    }

    public async Task<PamRotationConfigDetailResponseModel> PutAccount(Guid orgId, Guid id, UpdateRotationAccountRequestModel model)
    {
        await EnsureAdminAsync(orgId);

        var updated = await updateRotationAccountCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.AccountIdentity, model.TerminateSessions);
        return await GetDetailAsync(orgId, updated.Id);
    }

    public async Task Pause(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await pauseRotationCommand.PauseAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Resume(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await resumeRotationCommand.ResumeAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Rotate(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await triggerRotationCommand.TriggerAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task RecordManual(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await recordManualRotationCommand.RecordAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await EnsureAdminAsync(orgId);

        await deleteRotationConfigCommand.DeleteAsync(orgId, currentContext.UserId!.Value, id);
    }

    private async Task<PamRotationConfigDetailResponseModel> GetDetailAsync(Guid orgId, Guid id)
    {
        var history = await getRotationConfigDetailsQuery.GetAsync(orgId, id);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var awaitingManualRotation = PamRotationRules.AwaitingManualRotation(
            history.Config, history.Config.TargetSystemMethod, now);
        return new PamRotationConfigDetailResponseModel(history, awaitingManualRotation);
    }

    private async Task EnsureAdminAsync(Guid orgId)
    {
        if (!await currentContext.OrganizationAdmin(orgId) && !await currentContext.OrganizationOwner(orgId))
        {
            throw new NotFoundException();
        }
    }
}
