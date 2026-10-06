using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Pam;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IGetRotationCipherQuery" />
public class GetRotationCipherQuery : IGetRotationCipherQuery
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly TimeProvider _timeProvider;

    public GetRotationCipherQuery(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        ICipherRepository cipherRepository,
        TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _cipherRepository = cipherRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Cipher> GetAsync(Guid accessConnectorId, Guid attemptId)
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
        var jobStatus = PamRotationStatusDerivation.ComputeJobStatus(job.Action, job.ExpiresAt, now);
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

        return cipher;
    }
}
