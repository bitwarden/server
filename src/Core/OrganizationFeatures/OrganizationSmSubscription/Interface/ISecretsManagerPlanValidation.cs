using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;

public interface IValidateSecretsManagerPlan
{
    void ValidateSecretsManagerPlan(Models.StaticStore.Plan plan, Organization signup, int additionalSeats,
        int additionalServiceAccounts);
}
