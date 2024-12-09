namespace Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;

public interface ICountNewServiceAccountSlotsRequiredQuery
{
    Task<int> CountNewServiceAccountSlotsRequiredAsync(Guid organizationId, int serviceAccountsToAdd);
}
