using Bit.Core.Models.Business;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions.Interface;

public interface IUpdateSecretsManagerSubscriptionCommand
{
    Task UpdateSubscriptionAsync(SecretsManagerSubscriptionUpdate update);
}
