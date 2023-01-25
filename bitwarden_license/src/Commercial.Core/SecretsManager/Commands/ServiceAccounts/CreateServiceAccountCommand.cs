using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

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
