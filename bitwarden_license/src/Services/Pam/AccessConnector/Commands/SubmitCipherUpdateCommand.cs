using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="ISubmitCipherUpdateCommand" />
public class SubmitCipherUpdateCommand : ISubmitCipherUpdateCommand
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICipherSyncPushService _cipherSyncPushService;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SubmitCipherUpdateCommand(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        IPamDaemonRepository daemonRepository,
        ICipherRepository cipherRepository,
        ICipherSyncPushService cipherSyncPushService,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _daemonRepository = daemonRepository;
        _cipherRepository = cipherRepository;
        _cipherSyncPushService = cipherSyncPushService;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task SubmitAsync(Guid daemonId, Guid attemptId, string cipherDataJson, DateTime lastKnownRevisionDate)
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
        var outcome = await _jobRepository.AcceptCipherWriteAsync(
            attemptId, daemonId, cipherDataJson, lastKnownRevisionDate, now);

        if (outcome != PamRotationCipherWriteOutcome.Accepted)
        {
            var audit = new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.RotationCipherWriteRejected,
                OccurredAt = now,
                OrganizationId = config.OrganizationId,
                ActorId = null,
                DaemonId = daemonId,
                DaemonName = daemon.Name,
                RotationJobId = job?.Id,
                RotationConfigId = config.Id,
                CipherId = config.CipherId,
                Detail = outcome == PamRotationCipherWriteOutcome.RevisionMismatch
                    ? "The cipher was modified since it was last read; the write capability held but the revision date no longer matched."
                    : "The write capability no longer held: the job is not claimed by this daemon, or the attempt is not executing.",
            };
            await _accessAuditEventEmitter.EmitAsync(audit);

            throw new ConflictException(outcome == PamRotationCipherWriteOutcome.RevisionMismatch
                ? "The cipher has been modified since it was last read."
                : "This attempt can no longer write to the cipher.");
        }

        // Accepted has no dedicated audit kind of its own -- the eventual success/failure report is what the trail
        // records. Push a resync so open clients pick up the rotated secret; the durable signal for clients that
        // miss it is the account revision date PamRotationAttempt_AcceptCipherWrite bumps in the same transaction.
        var cipher = await _cipherRepository.GetByIdAsync(config.CipherId);
        if (cipher is not null)
        {
            await _cipherSyncPushService.PushSyncCipherUpdateAsync(cipher, []);
        }
    }
}
