using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;

public interface IUpdateServiceAccountCommand
{
    Task<ServiceAccount> UpdateAsync(ServiceAccount serviceAccount);
}
