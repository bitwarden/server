using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IUpdateSecretsManagerSubscriptionCommand
{
    Task UpdateSecretsManagerSubscription(SecretsManagerSubscriptionUpdate update);
}
