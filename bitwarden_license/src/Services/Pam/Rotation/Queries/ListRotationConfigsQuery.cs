using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Queries;

/// <inheritdoc cref="IListRotationConfigsQuery" />
public class ListRotationConfigsQuery : IListRotationConfigsQuery
{
    private readonly IPamRotationConfigRepository _configRepository;

    public ListRotationConfigsQuery(IPamRotationConfigRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public Task<ICollection<PamRotationConfigDetails>> ListAsync(Guid organizationId) =>
        _configRepository.GetManyDetailsByOrganizationIdAsync(organizationId);
}
