using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

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
        // Unknown attempt id: nothing to audit against (spec's `exists attempt` precondition). The attempt id is a
        // bare route value the daemon supplies, so an attempt in another organization has to be indistinguishable
        // from one that does not exist -- otherwise the reject audit below lands in the victim organization's trail
        // carrying this daemon's name, and the 404-vs-409 split tells the caller which foreign ids are real.
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        var job = attempt is null ? null : await _jobRepository.GetByIdAsync(attempt.JobId);
        var config = job is null ? null : await _configRepository.GetByIdAsync(job.RotationConfigId);
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);

        if (attempt is null || config is null || daemon is null || config.OrganizationId != daemon.OrganizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var outcome = await _jobRepository.MarkAttemptRotatedAsync(attemptId, daemonId, sessionTermination, now);

        if (outcome != PamRotationAttemptResolveOutcome.Resolved)
        {
            // Stale report (spec RejectStaleSuccess): nothing changed, but the report itself is worth auditing.
            var rejectedAudit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationReportRejected,
                OccurredAt = now,
                OrganizationId = config.OrganizationId,
                ActorId = null,
                DaemonId = daemonId,
                DaemonName = daemon.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config.Id,
                CipherId = config.CipherId,
                Detail = "Stale success report: the attempt is no longer executing under this daemon's claim.",
            };
            await _accessAuditEventEmitter.EmitAsync(rejectedAudit);

            throw new ConflictException("This attempt is no longer executing.");
        }

        config.LastRotationAt = now;
        config.NextRotationAt = _scheduleCalculator.GetNextOccurrence(config.ScheduleCron, now);
        config.RevisionDate = now;
        await _configRepository.ReplaceAsync(config);

        // Machinery event: single Outcome-phase, no human actor.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationSucceeded,
            OccurredAt = now,
            OrganizationId = config.OrganizationId,
            ActorId = null,
            DaemonId = daemonId,
            DaemonName = daemon.Name,
            RotationJobId = job?.Id,
            RotationConfigId = config.Id,
            CipherId = config.CipherId,
            RotationSource = job?.Source,
        };
        await _accessAuditEventEmitter.EmitAsync(audit);

        // Re-fetch: the repository just mutated the attempt's Status/ResolvedDate/SessionTermination under the
        // hood, and the caller expects the resolved snapshot back.
        return await _jobRepository.GetAttemptByIdAsync(attemptId) ?? attempt;
    }
}
