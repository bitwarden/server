using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;

public static class PasswordManagerInviteUserValidator
{
    // NOTE This is only for validating adding users to an organization, not removing
    public static ValidationResult<PasswordManagerSubscriptionUpdate> Validate(PasswordManagerSubscriptionUpdate subscriptionUpdate)
    {
        if (subscriptionUpdate.Seats is null)
        {
            return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
        }

        if (subscriptionUpdate.AdditionalSeats == 0)
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
        if (subscriptionUpdate.AdditionalSeats > subscriptionUpdate.PasswordManagerPlan.MaxAdditionalSeats)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerPlanOnlyAllowsMaxAdditionalSeatsError(subscriptionUpdate));
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
