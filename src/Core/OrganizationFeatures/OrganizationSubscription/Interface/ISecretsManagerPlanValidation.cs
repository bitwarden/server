using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;

public interface ISecretsManagerPlanValidation
{
    void ValidateSecretsManagerPlan(Models.StaticStore.Plan plan, Organization signup, int additionalSeats,
        int additionalServiceAccounts);
}
