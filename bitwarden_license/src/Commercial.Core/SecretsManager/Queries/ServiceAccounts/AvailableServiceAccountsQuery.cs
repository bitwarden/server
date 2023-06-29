using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;

public class AvailableServiceAccountsQuery : IAvailableServiceAccountsQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public AvailableServiceAccountsQuery(
        IOrganizationRepository organizationRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _organizationRepository = organizationRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<int> GetAvailableServiceAccountsAsync(Guid organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var serviceAccountCount = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organizationId);

        var availableServiceAccounts = Math.Max(0, organization.SmServiceAccounts.HasValue ? organization.SmServiceAccounts.Value - serviceAccountCount : 0);

        return availableServiceAccounts;
    }
}
