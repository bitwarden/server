using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Queries;

/// <inheritdoc cref="IListClaimableJobsQuery" />
public class ListClaimableJobsQuery : IListClaimableJobsQuery
{
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly TimeProvider _timeProvider;

    public ListClaimableJobsQuery(IPamRotationJobRepository jobRepository, TimeProvider timeProvider)
    {
        _jobRepository = jobRepository;
        _timeProvider = timeProvider;
    }

    public Task<ICollection<PamRotationJob>> ListAsync(Guid daemonId) =>
        _jobRepository.GetManyClaimableByDaemonIdAsync(daemonId, _timeProvider.GetUtcNow().UtcDateTime);
}
