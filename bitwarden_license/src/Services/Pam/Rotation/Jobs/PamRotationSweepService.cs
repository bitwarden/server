using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Jobs;

/// <inheritdoc cref="IPamRotationSweepService" />
public class PamRotationSweepService : IPamRotationSweepService
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IOfferRotationCommand _offerRotationCommand;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PamRotationSweepService> _logger;

    public PamRotationSweepService(
        IPamRotationConfigRepository configRepository,
        IPamRotationJobRepository jobRepository,
        IOfferRotationCommand offerRotationCommand,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider,
        ILogger<PamRotationSweepService> logger)
    {
        _configRepository = configRepository;
        _jobRepository = jobRepository;
        _offerRotationCommand = offerRotationCommand;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SweepAsync()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Each phase sweeps a disjoint set of rows -- a bug or transient failure in one (including the repository
        // call itself) must never prevent the other two from running, mirroring BaseJob's swallow-and-log philosophy
        // one level down.
        await RunPhaseAsync("due", () => SweepDueAsync(now));
        await RunPhaseAsync("timeouts", () => SweepTimeoutsAsync(now));
        await RunPhaseAsync("releases", () => SweepReleasesAsync(now));
    }

    private async Task RunPhaseAsync(string phase, Func<Task> runPhaseAsync)
    {
        try
        {
            await runPhaseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PamRotationSweepService: the {Phase} phase failed.", phase);
        }
    }

    private async Task SweepDueAsync(DateTime now)
    {
        var dueConfigs = await _configRepository.GetManyDueAsync(now);
        foreach (var config in dueConfigs)
        {
            try
            {
                // OfferAsync re-checks can_offer and no-ops silently on ActiveJobExists/ConfigNotOfferable -- the
                // sweep does not need to inspect the outcome, only isolate genuine failures per config.
                await _offerRotationCommand.OfferAsync(config.Id, PamRotationSource.Scheduled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PamRotationSweepService: failed to offer a due rotation for config {RotationConfigId}.",
                    config.Id);
            }
        }
    }

    private async Task SweepTimeoutsAsync(DateTime now)
    {
        var timedOutJobs = await _jobRepository.TimeoutDueAsync(now);
        foreach (var job in timedOutJobs)
        {
            try
            {
                var config = await _configRepository.GetByIdAsync(job.RotationConfigId);
                if (config is not null)
                {
                    config.NextRotationAt = now + _options.Value.FailureRetryDelay;
                    config.RevisionDate = now;
                    await _configRepository.ReplaceAsync(config);
                }

                // Machinery event: single Outcome-phase, no human actor.
                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.RotationJobTimedOut,
                    OccurredAt = now,
                    OrganizationId = job.OrganizationId,
                    ActorId = null,
                    CipherId = job.CipherId,
                    RotationConfigId = job.RotationConfigId,
                    RotationJobId = job.JobId,
                    RotationSource = job.Source,
                    DaemonId = job.ClaimedByDaemonId,
                    Detail = job.AttemptCount == 0
                        ? "rotation job timed out (unroutable: no eligible daemon)"
                        : "rotation job timed out (stuck daemon)",
                };
                await _accessAuditEventEmitter.EmitAsync(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PamRotationSweepService: failed to process timed-out rotation job {RotationJobId}.", job.JobId);
            }
        }
    }

    private async Task SweepReleasesAsync(DateTime now)
    {
        var releasedJobs = await _jobRepository.ReleaseExpiredLeasesAsync(
            now, _options.Value.DaemonOfflineAfter, _options.Value.ReleaseDelay);
        foreach (var job in releasedJobs)
        {
            try
            {
                // Machinery event: single Outcome-phase, no human actor. The job's claim fields were already
                // cleared by ReleaseExpiredLeasesAsync -- ClaimedByDaemonId here is the pre-clear value it returned.
                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.RotationJobReleased,
                    OccurredAt = now,
                    OrganizationId = job.OrganizationId,
                    ActorId = null,
                    CipherId = job.CipherId,
                    RotationConfigId = job.RotationConfigId,
                    RotationJobId = job.JobId,
                    RotationSource = job.Source,
                    DaemonId = job.ClaimedByDaemonId,
                };
                await _accessAuditEventEmitter.EmitAsync(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PamRotationSweepService: failed to process released rotation job {RotationJobId}.", job.JobId);
            }
        }
    }
}
