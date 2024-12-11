using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class UpdateServiceAccountCommand : IUpdateServiceAccountCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICurrentContext _currentContext;

    public UpdateServiceAccountCommand(
        IServiceAccountRepository serviceAccountRepository,
        ICurrentContext currentContext
    )
    {
        _serviceAccountRepository = serviceAccountRepository;
        _currentContext = currentContext;
    }

    public async Task<ServiceAccount> UpdateAsync(ServiceAccount updatedServiceAccount)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(updatedServiceAccount.Id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        serviceAccount.Name = updatedServiceAccount.Name;
        serviceAccount.RevisionDate = DateTime.UtcNow;

        await _serviceAccountRepository.ReplaceAsync(serviceAccount);
        return serviceAccount;
    }
}
