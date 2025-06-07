using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

#nullable enable

public interface IDeleteServiceAccountsCommand
{
    Task DeleteServiceAccounts(IEnumerable<ServiceAccount> serviceAccounts);
}

