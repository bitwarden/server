using Bit.Core.Models.StaticStore;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IStaticStoreWrapper
{
    List<Plan> SecretsManagerPlans { get; }
}
