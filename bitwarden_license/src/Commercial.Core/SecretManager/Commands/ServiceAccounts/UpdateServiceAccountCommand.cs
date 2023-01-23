using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretManager.Commands.ServiceAccounts;

public class UpdateServiceAccountCommand : IUpdateServiceAccountCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public UpdateServiceAccountCommand(IServiceAccountRepository serviceAccountRepository)
    {
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<ServiceAccount> UpdateAsync(ServiceAccount serviceAccount)
    {
        var existingServiceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccount.Id);
        if (existingServiceAccount == null)
        {
            throw new NotFoundException();
        }

        serviceAccount.OrganizationId = existingServiceAccount.OrganizationId;
        serviceAccount.CreationDate = existingServiceAccount.CreationDate;
        serviceAccount.RevisionDate = DateTime.UtcNow;

        await _serviceAccountRepository.ReplaceAsync(serviceAccount);
        return serviceAccount;
    }
}
