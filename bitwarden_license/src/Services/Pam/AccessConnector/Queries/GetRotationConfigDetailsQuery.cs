using Bit.Core.Exceptions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IGetRotationConfigDetailsQuery" />
public class GetRotationConfigDetailsQuery : IGetRotationConfigDetailsQuery
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly TimeProvider _timeProvider;

    public GetRotationConfigDetailsQuery(
        IPamRotationConfigRepository configRepository, IPamRotationJobRepository jobRepository,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _jobRepository = jobRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationConfigHistory> GetAsync(Guid organizationId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var jobs = await _jobRepository.GetManyByConfigIdAsync(configId, _timeProvider.GetUtcNow().UtcDateTime);

        return new PamRotationConfigHistory(details, jobs.ToList());
    }
}
