using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class AutoscaleServiceAccountsCommand : IAutoscaleServiceAccountsCommand
{
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;

    public AutoscaleServiceAccountsCommand(
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand)
    {
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
    }

    public async Task AutoscaleServiceAccountsAsync(Guid organizationId, int serviceAccountsToAdd)
    {
        await _updateSecretsManagerSubscriptionCommand.UpdateSecretsManagerSubscription(
            new SecretsManagerSubscriptionUpdate
            {
                OrganizationId = organizationId,
                SmServiceAccountsAdjustment = serviceAccountsToAdd
            });
    }
}
