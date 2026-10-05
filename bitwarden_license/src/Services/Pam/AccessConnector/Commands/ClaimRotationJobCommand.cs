using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IClaimRotationJobCommand" />
public class ClaimRotationJobCommand : IClaimRotationJobCommand
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ClaimRotationJobCommand(
        IPamRotationJobRepository jobRepository,
        IPamAccessConnectorRepository accessConnectorRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _accessConnectorRepository = accessConnectorRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationClaimResult> ClaimAsync(Guid accessConnectorId, Guid jobId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _jobRepository.ClaimAsync(jobId, accessConnectorId, now, _options.Value.ReleaseDelay);

        switch (result.Outcome)
        {
            case PamRotationClaimOutcome.Claimed:
                // The access connector's organization is the config's organization by construction of
                // EligibleClaimsOnly, so it is a cheap, correct stand-in for the audit's required OrganizationId.
                var accessConnector = await _accessConnectorRepository.GetByIdAsync(accessConnectorId);
                var job = await _jobRepository.GetByIdAsync(jobId);

                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.RotationDispatched,
                    OccurredDate = now,
                    OrganizationId = accessConnector?.OrganizationId ?? Guid.Empty,
                    ActorId = null,
                    AccessConnectorId = accessConnectorId,
                    AccessConnectorName = accessConnector?.Name,
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
                // Another access connector likely won the race -- 409, retry a different job.
                throw new ConflictException("This job is no longer claimable.");

            default:
                // NotEligible: never leak *why* -- looks the same as a job that never existed.
                throw new NotFoundException();
        }
    }
}
