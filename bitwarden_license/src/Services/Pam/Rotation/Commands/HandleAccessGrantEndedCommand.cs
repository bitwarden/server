using Bit.Core;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

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
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamRotation))
        {
            return;
        }

        var config = await _configRepository.GetByCipherIdAsync(cipherId);
        if (config is null || !config.RotateOnAccessEnd)
        {
            return;
        }

        // Paused/disabled is a no-op this iteration -- the deferred access-end latch (pending_access_end) would
        // otherwise remember this and discharge it on Enable/Resume.
        if (!config.Enabled)
        {
            return;
        }

        var target = await _targetSystemRepository.GetByIdAsync(config.TargetSystemId);
        if (target is null)
        {
            return;
        }

        if (target.Method == PamTargetSystemMethod.Automatic)
        {
            // OfferRotationCommand re-checks can_offer (including target Active and no active job) and no-ops
            // silently when it no longer holds -- no need to duplicate that guard here.
            await _offerRotationCommand.OfferAsync(config.Id, PamRotationSource.AccessEnd);
            return;
        }

        // Manual target: there is no daemon to offer a job to, so the obligation is pulled due immediately.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var toPersist = new PamRotationConfig
        {
            Id = config.Id,
            OrganizationId = config.OrganizationId,
            CipherId = config.CipherId,
            TargetSystemId = config.TargetSystemId,
            AccountIdentity = config.AccountIdentity,
            TerminateSessions = config.TerminateSessions,
            ScheduleCron = config.ScheduleCron,
            RotateOnAccessEnd = config.RotateOnAccessEnd,
            NextRotationAt = now,
            Enabled = config.Enabled,
            LastRotationAt = config.LastRotationAt,
            CreationDate = config.CreationDate,
            RevisionDate = now,
        };
        await _configRepository.ReplaceAsync(toPersist);

        // Machinery event: single Outcome-phase, no human actor.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.ManualRotationDue,
            OccurredAt = now,
            OrganizationId = config.OrganizationId,
            ActorId = null,
            CipherId = config.CipherId,
            RotationConfigId = config.Id,
            RotationSource = PamRotationSource.AccessEnd,
        };
        await _accessAuditEventEmitter.EmitAsync(audit);
    }
}
