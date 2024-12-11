using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class DeleteServiceAccountsCommand : IDeleteServiceAccountsCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public DeleteServiceAccountsCommand(IServiceAccountRepository serviceAccountRepository)
    {
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task DeleteServiceAccounts(IEnumerable<ServiceAccount> serviceAccounts)
    {
        await _serviceAccountRepository.DeleteManyByIdAsync(serviceAccounts.Select(sa => sa.Id));
    }
}
