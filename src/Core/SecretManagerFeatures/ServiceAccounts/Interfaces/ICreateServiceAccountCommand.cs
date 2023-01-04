using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;

public interface ICreateServiceAccountCommand
{
    Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount);
}
