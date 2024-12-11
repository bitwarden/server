using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;

public class ServiceAccountSecretsDetailsQuery : IServiceAccountSecretsDetailsQuery
{
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public ServiceAccountSecretsDetailsQuery(IServiceAccountRepository serviceAccountRepository)
    {
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<IEnumerable<ServiceAccountSecretsDetails>> GetManyByOrganizationIdAsync(
        Guid organizationId,
        Guid userId,
        AccessClientType accessClient,
        bool includeAccessToSecrets
    )
    {
        if (includeAccessToSecrets)
        {
            return await _serviceAccountRepository.GetManyByOrganizationIdWithSecretsDetailsAsync(
                organizationId,
                userId,
                accessClient
            );
        }

        var serviceAccounts = await _serviceAccountRepository.GetManyByOrganizationIdAsync(
            organizationId,
            userId,
            accessClient
        );

        return serviceAccounts.Select(sa => new ServiceAccountSecretsDetails
        {
            ServiceAccount = sa,
            AccessToSecrets = 0,
        });
    }
}
