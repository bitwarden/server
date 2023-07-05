using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class StaticStoreWrapper : IStaticStoreWrapper
{
    public List<Plan> SecretsManagerPlans { get; }

    public StaticStoreWrapper()
    {
        SecretsManagerPlans = StaticStore.SecretManagerPlans.ToList();
    }
}
