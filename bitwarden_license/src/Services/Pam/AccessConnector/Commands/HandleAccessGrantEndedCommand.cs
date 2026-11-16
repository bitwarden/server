using Bit.Core;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IHandleAccessGrantEndedCommand" />
public class HandleAccessGrantEndedCommand : IHandleAccessGrantEndedCommand
{
    private readonly IFeatureService _featureService;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IOfferRotationCommand _offerRotationCommand;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public HandleAccessGrantEndedCommand(
        IFeatureService featureService,
        IPamRotationConfigRepository configRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IOfferRotationCommand offerRotationCommand,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _configRepository = configRepository;
        _targetSystemRepository = targetSystemRepository;
        _offerRotationCommand = offerRotationCommand;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid cipherId)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamAccessConnector))
        {
            return;
        }

        var config = await _configRepository.GetByCipherIdAsync(cipherId);
        if (config is null)
        {
            return;
        }

        // Dropped while paused, pending access end included; the spec's DeferAccessEndWhileUnavailable is not
        // implemented.
        if (!config.RotateOnAccessEnd || !config.Enabled)
        {
            await _configRepository.DeletePendingAccessEndAsync(config.Id);
            return;
        }

        var target = await _targetSystemRepository.GetByIdAsync(config.TargetSystemId);
        if (target is null)
        {
            return;
        }

        if (target.Method == PamTargetSystemMethod.Automatic)
        {
            // OfferAsync re-checks can_offer and no-ops when it does not hold.
            await _offerRotationCommand.OfferAsync(config.Id, PamRotationSource.AccessEnd);
            // Cleared only once the offer returns, so the rotation sweep retries one that throws.
            await _configRepository.DeletePendingAccessEndAsync(config.Id);
            return;
        }

        // A manual target has no access connector to offer a job to, so its rotation is pulled due now. False means
        // nothing was pending, for example because a concurrent call already raised it.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!await _configRepository.RaiseManualObligationOnAccessEndAsync(config.Id, now))
        {
            return;
        }

        // Machinery event: single Outcome-phase, no human actor.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.ManualRotationDue,
            OccurredDate = now,
            OrganizationId = config.OrganizationId,
            ActorId = null,
            CipherId = config.CipherId,
            RotationConfigId = config.Id,
            RotationSource = PamRotationSource.AccessEnd,
        };
        await _accessAuditEventEmitter.EmitAsync(audit);
    }
}
