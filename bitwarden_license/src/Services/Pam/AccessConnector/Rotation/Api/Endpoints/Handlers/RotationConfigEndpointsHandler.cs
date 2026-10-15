using Bit.Core.Context;
using Bit.Core.Exceptions;
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
/// middleware by <c>ManageAccessConnectorRequirement</c>. Writes also need edit access to the config's cipher, and
/// re-read the detail view, since the commands return the bare entity.
/// </summary>
public class RotationConfigEndpointsHandler(
    ICurrentContext currentContext,
    TimeProvider timeProvider,
    IPamRotationConfigRepository configRepository,
    IGetRotationConfigDetailsQuery getRotationConfigDetailsQuery,
    ICreateRotationConfigCommand createRotationConfigCommand,
    IUpdateRotationConfigCommand updateRotationConfigCommand,
    IPauseRotationCommand pauseRotationCommand,
    IResumeRotationCommand resumeRotationCommand,
    ITriggerRotationCommand triggerRotationCommand,
    IRecordManualRotationCommand recordManualRotationCommand,
    IDeleteRotationConfigCommand deleteRotationConfigCommand,
    ICanEditRotationCiphersQuery canEditRotationCiphersQuery)
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
        await EnsureCanEditCipherAsync(orgId, model.CipherId);
        var created = await createRotationConfigCommand.CreateAsync(
            orgId,
            currentContext.UserId!.Value,
            model.CipherId,
            model.TargetSystemId,
            model.SealedData,
            model.WrappedKey,
            model.ScheduleCron,
            model.RotateOnAccessEnd);
        return await GetDetailAsync(orgId, created.Id);
    }

    public async Task<PamRotationConfigDetailResponseModel> Put(
        Guid orgId, Guid id, UpdateRotationConfigRequestModel model)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        var updated = await updateRotationConfigCommand.UpdateAsync(
            orgId, currentContext.UserId!.Value, id, model.SealedData, model.WrappedKey, model.ScheduleCron, model.RotateOnAccessEnd);
        return await GetDetailAsync(orgId, updated.Id);
    }

    public async Task Pause(Guid orgId, Guid id)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        await pauseRotationCommand.PauseAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Resume(Guid orgId, Guid id)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        await resumeRotationCommand.ResumeAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Rotate(Guid orgId, Guid id)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        await triggerRotationCommand.TriggerAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task RecordManual(Guid orgId, Guid id)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        await recordManualRotationCommand.RecordAsync(orgId, currentContext.UserId!.Value, id);
    }

    public async Task Delete(Guid orgId, Guid id)
    {
        await EnsureCanEditConfigCipherAsync(orgId, id);
        await deleteRotationConfigCommand.DeleteAsync(orgId, currentContext.UserId!.Value, id);
    }

    private async Task EnsureCanEditConfigCipherAsync(Guid orgId, Guid configId)
    {
        var config = await configRepository.GetByIdAsync(configId);
        if (config is null || config.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await EnsureCanEditCipherAsync(orgId, config.CipherId);
    }

    private async Task EnsureCanEditCipherAsync(Guid orgId, Guid cipherId)
    {
        if (!await canEditRotationCiphersQuery.CanEditAllAsync(orgId, [cipherId]))
        {
            throw new NotFoundException();
        }
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
