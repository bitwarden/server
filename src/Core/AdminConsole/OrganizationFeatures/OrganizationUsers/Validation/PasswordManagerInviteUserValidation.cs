namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public static class PasswordManagerInviteUserValidation
{

    // TODO need to add plan validation from AdjustSeatsAsync

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
            return new Invalid<PasswordManagerSubscriptionUpdate>(InviteUserValidationErrorMessages
                .SeatLimitHasBeenReachedError);
        }

        return new Valid<PasswordManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
