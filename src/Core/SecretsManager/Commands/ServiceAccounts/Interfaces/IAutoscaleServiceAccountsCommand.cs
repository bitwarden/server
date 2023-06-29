namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IAutoscaleServiceAccountsCommand
{
    Task<string> AutoscaleServiceAccountsAsync(Guid organizationId, int serviceAccountsToAdd);
}
