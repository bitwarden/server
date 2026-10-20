using Bit.Core.Exceptions;
using Bit.Core.Pam.AccessConnector.Queries;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;
using Bit.Pam;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IGetRotationCipherQuery" />
public class GetRotationCipherQuery : IGetRotationCipherQuery
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICipherLeaseGate _cipherLeaseGate;
    private readonly TimeProvider _timeProvider;

    public GetRotationCipherQuery(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        ICipherRepository cipherRepository,
        ICipherLeaseGate cipherLeaseGate,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _cipherRepository = cipherRepository;
        _cipherLeaseGate = cipherLeaseGate;
        _timeProvider = timeProvider;
    }

    public async Task<RotationCipherAccess> GetAsync(Guid accessConnectorId, Guid attemptId)
    {
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        if (attempt is null || attempt.ClaimedByAccessConnectorId != accessConnectorId)
        {
            throw new NotFoundException();
        }

        var job = await _jobRepository.GetByIdAsync(attempt.JobId);
        if (job is null || job.ClaimedByAccessConnectorId != accessConnectorId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var jobStatus = PamRotationStatusDerivation.ComputeJobStatus(job.Action, job.ExpirationDate, now);
        var attemptStatus = PamRotationStatusDerivation.ComputeAttemptStatus(
            attempt.Action, PamRotationRules.IsCurrentAttempt(job, attempt), jobStatus);
        if (attemptStatus != PamRotationAttemptStatus.Executing)
        {
            throw new NotFoundException();
        }

        var config = await _configRepository.GetByIdAsync(job.RotationConfigId);
        if (config is null)
        {
            throw new NotFoundException();
        }

        var cipher = await _cipherRepository.GetByIdAsync(config.CipherId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        // Every capability check above passed, so the attempt, not a lease, authorizes the full read.
        return new RotationCipherAccess(cipher, _cipherLeaseGate.AuthorizeAccessConnectorRotationRead(cipher));
    }
}
