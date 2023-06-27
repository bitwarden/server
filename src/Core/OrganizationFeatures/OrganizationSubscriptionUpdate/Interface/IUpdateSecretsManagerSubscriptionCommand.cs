using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IOrganizationUpdateSubscription
{
    Task UpdateSecretsManagerSubscription(OrganizationUpdate update);
}
