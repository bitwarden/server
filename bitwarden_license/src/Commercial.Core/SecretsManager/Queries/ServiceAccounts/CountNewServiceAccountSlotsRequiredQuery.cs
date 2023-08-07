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
        if (organization == null || !organization.UseSecretsManager)
        {
            throw new NotFoundException();
        }

        if (!organization.SmServiceAccounts.HasValue || serviceAccountsToAdd == 0 || organization.SecretsManagerBeta)
        {
            return 0;
        }

        var serviceAccountCount = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organizationId);
        var availableServiceAccountSlots = organization.SmServiceAccounts.Value - serviceAccountCount;

        if (availableServiceAccountSlots >= serviceAccountsToAdd)
        {
            return 0;
        }

        return serviceAccountsToAdd - availableServiceAccountSlots;
    }
}
