using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class SecretsManagerInviteUserValidation
{
    // NOTE This is only validating adding new users
    public static ValidationResult<SecretsManagerSubscriptionUpdate> Validate(SecretsManagerSubscriptionUpdate subscriptionUpdate)
    {
        if (subscriptionUpdate.UseSecretsManger is false)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(OrganizationNoSecretsManager);
        }

        if (subscriptionUpdate.Seats == null)
        {
            return new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate); // no need to adjust seats...continue on
        }

        // max additional seats is never set...maybe remove this
        if (subscriptionUpdate.SecretsManagerPlan is { HasAdditionalSeatsOption: false } ||
            subscriptionUpdate.SecretsManagerPlan.MaxAdditionalSeats is not null &&
            subscriptionUpdate.AdditionalSeats > subscriptionUpdate.SecretsManagerPlan.MaxAdditionalSeats)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(
                string.Format(SecretsManagerAdditionalSeatLimitReached,
                    subscriptionUpdate.SecretsManagerPlan.BaseSeats +
                    subscriptionUpdate.SecretsManagerPlan.MaxAdditionalSeats.GetValueOrDefault()));
        }

        if (subscriptionUpdate.UpdatedSeatTotal is not null && subscriptionUpdate.MaxAutoScaleSeats is not null &&
            subscriptionUpdate.UpdatedSeatTotal > subscriptionUpdate.MaxAutoScaleSeats)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(SecretsManagerSeatLimitReached);
        }

        if (subscriptionUpdate.PasswordManagerUpdatedSeatTotal < subscriptionUpdate.UpdatedSeatTotal)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(SecretsManagerCannotExceedPasswordManager);
        }

        return new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
