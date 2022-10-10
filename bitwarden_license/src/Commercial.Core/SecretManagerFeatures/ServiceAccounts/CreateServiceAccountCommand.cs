using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.ServiceAccounts;

public class CreateServiceAccountCommand : ICreateServiceAccountCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CreateServiceAccountCommand(IServiceAccountRepository serviceAccountRepository)
    {
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount)
    {
        return await _serviceAccountRepository.CreateAsync(serviceAccount);
    }
}
