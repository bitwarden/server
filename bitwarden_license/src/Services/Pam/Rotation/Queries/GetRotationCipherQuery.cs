using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Queries;

/// <inheritdoc cref="IGetRotationCipherQuery" />
public class GetRotationCipherQuery : IGetRotationCipherQuery
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly ICipherRepository _cipherRepository;

    public GetRotationCipherQuery(
        IPamRotationJobRepository jobRepository,
        IPamRotationConfigRepository configRepository,
        ICipherRepository cipherRepository)
    {
        _jobRepository = jobRepository;
        _configRepository = configRepository;
        _cipherRepository = cipherRepository;
    }

    public async Task<Cipher> GetAsync(Guid daemonId, Guid attemptId)
    {
        var attempt = await _jobRepository.GetAttemptByIdAsync(attemptId);
        if (attempt is null
            || attempt.ClaimedByDaemonId != daemonId
            || attempt.Status != PamRotationAttemptStatus.Executing)
        {
            throw new NotFoundException();
        }

        var job = await _jobRepository.GetByIdAsync(attempt.JobId);
        if (job is null || job.Status != PamRotationJobStatus.Claimed || job.ClaimedByDaemonId != daemonId)
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
