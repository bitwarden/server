namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IAutoscaleServiceAccountsCommand
{
    Task AutoscaleServiceAccountsAsync(Guid organizationId, int serviceAccountsToAdd);
}
