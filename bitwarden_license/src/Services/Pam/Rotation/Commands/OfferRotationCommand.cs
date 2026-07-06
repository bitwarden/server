using Bit.Pam;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IOfferRotationCommand" />
public class OfferRotationCommand : IOfferRotationCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public OfferRotationCommand(
        IPamRotationConfigRepository configRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IPamRotationJobRepository jobRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _targetSystemRepository = targetSystemRepository;
        _jobRepository = jobRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationJobCreateOutcome> OfferAsync(Guid configId, PamRotationSource source)
    {
        var config = await _configRepository.GetByIdAsync(configId);
        if (config is null)
        {
            // A concurrent delete raced this offer. Callers (the sweep, TriggerRotationCommand, the access-end
            // handler) all treat this the same as any other not-offerable outcome and move on silently.
            return PamRotationJobCreateOutcome.ConfigNotOfferable;
        }

        var target = await _targetSystemRepository.GetByIdAsync(config.TargetSystemId);
        if (target is null || !PamRotationRules.CanOffer(config, target.Method, target.Status))
        {
            return PamRotationJobCreateOutcome.ConfigNotOfferable;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var job = new PamRotationJob
        {
            RotationConfigId = configId,
            Source = source,
            Status = PamRotationJobStatus.Pending,
            ClaimedByDaemonId = null,
            ClaimedAt = null,
            CreationDate = now,
            NextClaimableAt = now,
            ExpiresAt = now + _options.Value.JobTtl,
        };
        // CreateGuardedAsync is a guarded custom insert, not the generic single-object CreateAsync -- it expects the
        // id to already be assigned.
        job.SetNewId();

        var outcome = await _jobRepository.CreateGuardedAsync(job);
        if (outcome == PamRotationJobCreateOutcome.Created)
        {
            // Machinery event: single Outcome-phase, no human actor.
            var audit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationOffered,
                OccurredAt = now,
                OrganizationId = config.OrganizationId,
                ActorId = null,
                CipherId = config.CipherId,
                RotationConfigId = config.Id,
                RotationJobId = job.Id,
                TargetSystemId = target.Id,
                TargetSystemName = target.Name,
                RotationSource = source,
            };
            await _accessAuditEventEmitter.EmitAsync(audit);
        }

        return outcome;
    }
}
