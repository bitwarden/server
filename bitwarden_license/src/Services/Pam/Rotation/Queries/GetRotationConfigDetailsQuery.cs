using Bit.Core.Exceptions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Models;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Queries;

/// <inheritdoc cref="IGetRotationConfigDetailsQuery" />
public class GetRotationConfigDetailsQuery : IGetRotationConfigDetailsQuery
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamRotationJobRepository _jobRepository;

    public GetRotationConfigDetailsQuery(
        IPamRotationConfigRepository configRepository, IPamRotationJobRepository jobRepository)
    {
        _configRepository = configRepository;
        _jobRepository = jobRepository;
    }

    public async Task<PamRotationConfigHistory> GetAsync(Guid organizationId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var jobs = await _jobRepository.GetManyByConfigIdAsync(configId);

        return new PamRotationConfigHistory(details, jobs.ToList());
    }
}
