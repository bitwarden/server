using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Queries;

/// <inheritdoc cref="IListTargetSystemsQuery" />
public class ListTargetSystemsQuery : IListTargetSystemsQuery
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;

    public ListTargetSystemsQuery(IPamTargetSystemRepository targetSystemRepository)
    {
        _targetSystemRepository = targetSystemRepository;
    }

    public Task<ICollection<PamTargetSystem>> ListAsync(Guid organizationId) =>
        _targetSystemRepository.GetManyByOrganizationIdAsync(organizationId);
}
