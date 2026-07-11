using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IReportRotationFailedCommand" />
public class ReportRotationFailedCommand : IReportRotationFailedCommand
{
    private const int FailureReasonMaxLength = 500;

    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ReportRotationFailedCommand(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        IPamDaemonRepository daemonRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _daemonRepository = daemonRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationAttempt> ReportFailedAsync(
        Guid daemonId, Guid attemptId, string? failureReason, PamRotationSyncState syncState)
    {
        // Truncate before anything else -- the contract forbids forwarding raw target-system error output (it can
        // echo credentials), and truncation never rejects the report.
        var truncatedReason = Truncate(failureReason);

        // Unknown attempt id: nothing to audit against (spec's `exists attempt` precondition).
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        if (attempt is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _jobRepository.MarkAttemptErroredAsync(
            attemptId, daemonId, truncatedReason, syncState, now, _options.Value.MaxAttempts,
            _options.Value.RetryBaseDelay);

        var job = await _jobRepository.GetByIdAsync(attempt.JobId);
        var config = job is null ? null : await _configRepository.GetByIdAsync(job.RotationConfigId);
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);

        if (result.Outcome != PamRotationAttemptResolveOutcome.Resolved)
        {
            // Stale report (spec RejectStaleFailureReport): nothing changed, but the report itself is worth auditing.
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
                Detail = "Stale failure report: the attempt is no longer executing under this daemon's claim.",
            };
            await _accessAuditEventEmitter.EmitAsync(rejectedAudit);

            throw new ConflictException("This attempt is no longer executing.");
        }

        var organizationId = config?.OrganizationId ?? daemon?.OrganizationId ?? Guid.Empty;

        if (result.JobStatus == PamRotationJobStatus.Failed)
        {
            // Retry budget exhausted: the job failed outright, so the config's next rotation is pushed out rather
            // than immediately retried.
            if (config is not null)
            {
                config.NextRotationAt = now + _options.Value.FailureRetryDelay;
                config.RevisionDate = now;
                await _configRepository.ReplaceAsync(config);
            }

            var failedAudit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationFailed,
                OccurredAt = now,
                OrganizationId = organizationId,
                ActorId = null,
                DaemonId = daemonId,
                DaemonName = daemon?.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config?.Id,
                CipherId = config?.CipherId,
                RotationSource = job?.Source,
                SyncState = syncState,
                Detail = truncatedReason,
            };
            await _accessAuditEventEmitter.EmitAsync(failedAudit);
        }
        else
        {
            // Retry budget remains: the job went back to Pending for another attempt.
            var attemptFailedAudit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationAttemptFailed,
                OccurredAt = now,
                OrganizationId = organizationId,
                ActorId = null,
                DaemonId = daemonId,
                DaemonName = daemon?.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config?.Id,
                CipherId = config?.CipherId,
                RotationSource = job?.Source,
                SyncState = syncState,
                Detail = truncatedReason,
            };
            await _accessAuditEventEmitter.EmitAsync(attemptFailedAudit);
        }

        // Re-fetch: the repository just mutated the attempt's Status/FailureReason/SyncState/ResolvedDate under the
        // hood, and the caller expects the resolved snapshot back.
        return await _jobRepository.GetAttemptByIdAsync(attemptId) ?? attempt;
    }

    private static string? Truncate(string? failureReason) =>
        failureReason is not null && failureReason.Length > FailureReasonMaxLength
            ? failureReason[..FailureReasonMaxLength]
            : failureReason;
}
