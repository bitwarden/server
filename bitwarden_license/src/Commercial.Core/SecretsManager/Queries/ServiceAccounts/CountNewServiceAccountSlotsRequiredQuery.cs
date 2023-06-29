using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;

public class CountNewServiceAccountSlotsRequiredQuery : ICountNewServiceAccountSlotsRequiredQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CountNewServiceAccountSlotsRequiredQuery(
        IOrganizationRepository organizationRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _organizationRepository = organizationRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<int> CountNewServiceAccountSlotsRequiredAsync(Guid organizationId, int serviceAccountsToAdd)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!organization.SmServiceAccounts.HasValue)
        {
            return 0;
        }

        var serviceAccountCount = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organizationId);
        var availableServiceAccountSlots = Math.Max(0, organization.SmServiceAccounts.Value - serviceAccountCount);

        return Math.Max(0, serviceAccountsToAdd - availableServiceAccountSlots);
    }
}
