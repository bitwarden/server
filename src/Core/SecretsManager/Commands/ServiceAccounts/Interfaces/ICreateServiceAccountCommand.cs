using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

#nullable enable

public interface ICreateServiceAccountCommand
{
    Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount, Guid userId);
}
