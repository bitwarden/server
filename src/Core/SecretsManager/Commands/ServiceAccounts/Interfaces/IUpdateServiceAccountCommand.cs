using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IUpdateServiceAccountCommand
{
    Task<ServiceAccount> UpdateAsync(ServiceAccount serviceAccount, Guid userId);
}
