using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IDeleteServiceAccountsCommand
{
    Task DeleteServiceAccounts(IEnumerable<ServiceAccount> serviceAccounts);
}
