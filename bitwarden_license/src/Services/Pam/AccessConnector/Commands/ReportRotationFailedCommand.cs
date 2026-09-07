using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Commands;

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
        // Truncated first: raw target-system error output can echo credentials and must never be forwarded.
        var truncatedReason = Truncate(failureReason);

        // A cross-org attempt id must be indistinguishable from an unknown one, so no other org's trail leaks this daemon's name.
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        var job = attempt is null ? null : await _jobRepository.GetByIdAsync(attempt.JobId);
        var config = job is null ? null : await _configRepository.GetByIdAsync(job.RotationConfigId);
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);

        if (attempt is null || config is null || daemon is null || config.OrganizationId != daemon.OrganizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _jobRepository.MarkAttemptErroredAsync(
            attemptId, daemonId, truncatedReason, syncState, now, _options.Value.MaxAttempts,
            _options.Value.RetryBaseDelay);

        if (result.Outcome != PamRotationAttemptResolveOutcome.Resolved)
        {
            // Stale report (spec RejectStaleFailureReport): nothing changed, but the report itself is worth auditing.
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
                Detail = "Stale failure report: the attempt is no longer executing under this daemon's claim.",
            };
            await _accessAuditEventEmitter.EmitAsync(rejectedAudit);

            throw new ConflictException("This attempt is no longer executing.");
        }

        var organizationId = config.OrganizationId;

        if (result.JobStatus == PamRotationJobStatus.Failed)
        {
            // Retry budget exhausted: push the next rotation out instead of retrying immediately, if scheduled.
            if (config.ScheduleCron is not null)
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
                DaemonName = daemon.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config.Id,
                CipherId = config.CipherId,
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
                DaemonName = daemon.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config.Id,
                CipherId = config.CipherId,
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
