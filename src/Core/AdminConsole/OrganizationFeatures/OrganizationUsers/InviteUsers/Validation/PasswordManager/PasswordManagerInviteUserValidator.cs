using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public static class PasswordManagerInviteUserValidator
{
    /// <summary>
    /// This is for validating if the organization can add additional users.
    /// </summary>
    /// <param name="subscriptionUpdate"></param>
    /// <returns></returns>
    public static ValidationResult<PasswordManagerSubscriptionUpdate> Validate(PasswordManagerSubscriptionUpdate subscriptionUpdate)
    {
        if (subscriptionUpdate.Seats is null)
        {
            return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
        }

        if (subscriptionUpdate.NewUsersToAdd == 0)
        {
            return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
        }

        if (subscriptionUpdate.UpdatedSeatTotal is not null && subscriptionUpdate.MaxAutoScaleSeats is not null &&
            subscriptionUpdate.UpdatedSeatTotal > subscriptionUpdate.MaxAutoScaleSeats)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerSeatLimitHasBeenReachedError(subscriptionUpdate));
        }

        if (subscriptionUpdate.PasswordManagerPlan.HasAdditionalSeatsOption is false)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerPlanDoesNotAllowAdditionalSeatsError(subscriptionUpdate));
        }

        // Apparently MaxAdditionalSeats is never set. Can probably be removed.
        if (subscriptionUpdate.NewUsersToAdd > subscriptionUpdate.PasswordManagerPlan.MaxAdditionalSeats)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerPlanOnlyAllowsMaxAdditionalSeatsError(subscriptionUpdate));
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
