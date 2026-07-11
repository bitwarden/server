using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IClaimRotationJobCommand" />
public class ClaimRotationJobCommand : IClaimRotationJobCommand
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ClaimRotationJobCommand(
        IPamRotationJobRepository jobRepository,
        IPamDaemonRepository daemonRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _daemonRepository = daemonRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationClaimResult> ClaimAsync(Guid daemonId, Guid jobId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _jobRepository.ClaimAsync(jobId, daemonId, now, _options.Value.ReleaseDelay);

        switch (result.Outcome)
        {
            case PamRotationClaimOutcome.Claimed:
                // The daemon's organization is the config's organization by construction of EligibleClaimsOnly, so
                // it is a cheap, correct stand-in for the audit's required OrganizationId.
                var daemon = await _daemonRepository.GetByIdAsync(daemonId);
                var job = await _jobRepository.GetByIdAsync(jobId);

                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.RotationDispatched,
                    OccurredAt = now,
                    OrganizationId = daemon?.OrganizationId ?? Guid.Empty,
                    ActorId = null,
                    DaemonId = daemonId,
                    DaemonName = daemon?.Name,
                    RotationJobId = jobId,
                    RotationConfigId = job?.RotationConfigId,
                    CipherId = result.CipherId,
                    TargetSystemId = result.TargetSystemId,
                    TargetSystemName = result.TargetSystemName,
                    RotationSource = result.Source,
                };
                await _accessAuditEventEmitter.EmitAsync(audit);
                return result;

            case PamRotationClaimOutcome.NotClaimable:
                // Another daemon likely won the race -- 409, retry a different job.
                throw new ConflictException("This job is no longer claimable.");

            default:
                // NotEligible: never leak *why* -- looks the same as a job that never existed.
                throw new NotFoundException();
        }
    }
}
