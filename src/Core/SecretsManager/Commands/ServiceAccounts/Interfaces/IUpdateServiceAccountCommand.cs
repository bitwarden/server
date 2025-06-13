using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

#nullable enable

public interface IUpdateServiceAccountCommand
{
    Task<ServiceAccount> UpdateAsync(ServiceAccount serviceAccount);
}
