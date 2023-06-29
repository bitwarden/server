namespace Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;

public interface IAvailableServiceAccountsQuery
{
    Task<int> GetAvailableServiceAccountsAsync(Guid organizationId);
}
