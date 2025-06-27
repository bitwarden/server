namespace Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;

#nullable enable

public interface ICountNewServiceAccountSlotsRequiredQuery
{
    Task<int> CountNewServiceAccountSlotsRequiredAsync(Guid organizationId, int serviceAccountsToAdd);
}
