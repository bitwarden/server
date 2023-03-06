using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IDeleteServiceAccountsCommand
{
    Task<List<Tuple<ServiceAccount, string>>> DeleteServiceAccounts(List<Guid> ids, Guid userId);
}

