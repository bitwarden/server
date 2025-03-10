using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class PasswordManagerInviteUserValidation
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
            return new Invalid<PasswordManagerSubscriptionUpdate>(SeatLimitHasBeenReachedError);
        }

        if (subscriptionUpdate.PasswordManagerPlan.HasAdditionalSeatsOption is false)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(PlanDoesNotAllowAdditionalSeats);
        }

        // Apparently MaxAdditionalSeats is never set. Can probably be removed.
        if (subscriptionUpdate.AdditionalSeats > subscriptionUpdate.PasswordManagerPlan.MaxAdditionalSeats)
        {
            return new Invalid<PasswordManagerSubscriptionUpdate>(string.Format(PlanOnlyAllowsMaxAdditionalSeats,
                subscriptionUpdate.PasswordManagerPlan.MaxAdditionalSeats));
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
