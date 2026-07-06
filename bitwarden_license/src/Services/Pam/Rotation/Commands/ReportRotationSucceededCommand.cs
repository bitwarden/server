using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IReportRotationSucceededCommand" />
public class ReportRotationSucceededCommand : IReportRotationSucceededCommand
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public ReportRotationSucceededCommand(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        IPamDaemonRepository daemonRepository,
        IRotationScheduleCalculator scheduleCalculator,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _daemonRepository = daemonRepository;
        _scheduleCalculator = scheduleCalculator;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationAttempt> ReportSucceededAsync(
        Guid daemonId, Guid attemptId, PamSessionTerminationOutcome sessionTermination)
    {
        // Unknown attempt id: nothing to audit against (spec's `exists attempt` precondition).
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        if (attempt is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var outcome = await _jobRepository.MarkAttemptRotatedAsync(attemptId, daemonId, sessionTermination, now);

        var job = await _jobRepository.GetByIdAsync(attempt.JobId);
        var config = job is null ? null : await _configRepository.GetByIdAsync(job.RotationConfigId);
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);

        if (outcome != PamRotationAttemptResolveOutcome.Resolved)
        {
            // Stale report (spec RejectStaleSuccess): nothing changed, but the report itself is worth auditing.
            var rejectedAudit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationReportRejected,
                OccurredAt = now,
                OrganizationId = config?.OrganizationId ?? Guid.Empty,
                ActorId = null,
                DaemonId = daemonId,
                DaemonName = daemon?.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config?.Id,
                CipherId = config?.CipherId,
                Detail = "Stale success report: the attempt is no longer executing under this daemon's claim.",
            };
            await _accessAuditEventEmitter.EmitAsync(rejectedAudit);

            throw new ConflictException("This attempt is no longer executing.");
        }

        if (config is not null)
        {
            config.LastRotationAt = now;
            config.NextRotationAt = _scheduleCalculator.GetNextOccurrence(config.ScheduleCron, now);
            config.RevisionDate = now;
            await _configRepository.ReplaceAsync(config);
        }

        // Machinery event: single Outcome-phase, no human actor.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationSucceeded,
            OccurredAt = now,
            OrganizationId = config?.OrganizationId ?? daemon?.OrganizationId ?? Guid.Empty,
            ActorId = null,
            DaemonId = daemonId,
            DaemonName = daemon?.Name,
            RotationJobId = job?.Id,
            RotationConfigId = config?.Id,
            CipherId = config?.CipherId,
            RotationSource = job?.Source,
        };
        await _accessAuditEventEmitter.EmitAsync(audit);

        // Re-fetch: the repository just mutated the attempt's Status/ResolvedDate/SessionTermination under the
        // hood, and the caller expects the resolved snapshot back.
        return await _jobRepository.GetAttemptByIdAsync(attemptId) ?? attempt;
    }
}
