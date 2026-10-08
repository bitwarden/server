using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/access-connectors/rotation/configs</c> resource, authorized in the
/// middleware by <c>ManageAccessConnectorRequirement</c>. Writes re-read the detail view, since the commands return
/// the bare entity.
/// </summary>
public class RotationConfigEndpointsHandler(
    ICurrentContext currentContext,
    TimeProvider timeProvider,
    IPamRotationConfigRepository configRepository,
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
        var configs = await configRepository.GetManyDetailsByOrganizationIdAsync(orgId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new ListResponseModel<PamRotationConfigResponseModel>(
            configs.Select(config => new PamRotationConfigResponseModel(
                config, PamRotationRules.AwaitingManualRotation(config, config.TargetSystemMethod, now))));
    }

    public async Task<PamRotationConfigDetailResponseModel> Get(Guid orgId, Guid id)
    {
        return await GetDetailAsync(orgId, id);
    }

    public async Task<PamRotationConfigDetailResponseModel> Post(Guid orgId, CreateRotationConfigRequestModel model)
    {
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

    /// <remarks>Fans out to the account and settings commands, account first since its guards are stricter.</remarks>
    public async Task<PamRotationConfigDetailResponseModel> Put(
        Guid orgId, Guid id, UpdateRotationConfigRequestModel model)
    {
        await updateRotationAccountCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.AccountIdentity, model.TerminateSessions);
        var updated = await updateRotationSettingsCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.ScheduleCron, model.RotateOnAccessEnd);
        return await GetDetailAsync(orgId, updated.Id);
    }

    public async Task Pause(Guid orgId, Guid id)
    {
        await pauseRotationCommand.PauseAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Resume(Guid orgId, Guid id)
    {
        await resumeRotationCommand.ResumeAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Rotate(Guid orgId, Guid id)
    {
        await triggerRotationCommand.TriggerAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task RecordManual(Guid orgId, Guid id)
    {
        await recordManualRotationCommand.RecordAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
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
}
