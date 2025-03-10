using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteUserValidationErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public static class SecretsManagerInviteUserValidation
{
    public static ValidationResult<SecretsManagerSubscriptionUpdate> Validate(
        SecretsManagerSubscriptionUpdate subscriptionUpdate) =>
        subscriptionUpdate switch
        {
            { UseSecretsManger: false, AdditionalSeats: > 0 } =>
                new Invalid<SecretsManagerSubscriptionUpdate>(OrganizationNoSecretsManager),

            { UseSecretsManger: false, AdditionalSeats: 0 } or { UseSecretsManger: true, Seats: null } =>
                new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate),

            { UseSecretsManger: true, SecretsManagerPlan.HasAdditionalSeatsOption: false } =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    string.Format(SecretsManagerAdditionalSeatLimitReached,
                    subscriptionUpdate.SecretsManagerPlan.BaseSeats +
                    subscriptionUpdate.SecretsManagerPlan.MaxAdditionalSeats.GetValueOrDefault())),

            { UseSecretsManger: true, SecretsManagerPlan.MaxAdditionalSeats: var planMaxSeats }
                when planMaxSeats < subscriptionUpdate.AdditionalSeats =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    string.Format(SecretsManagerAdditionalSeatLimitReached,
                        subscriptionUpdate.SecretsManagerPlan.BaseSeats +
                        subscriptionUpdate.SecretsManagerPlan.MaxAdditionalSeats.GetValueOrDefault())),

            { UseSecretsManger: true, UpdatedSeatTotal: var updateSeatTotal, MaxAutoScaleSeats: var maxAutoScaleSeats }
                when updateSeatTotal > maxAutoScaleSeats =>
                new Invalid<SecretsManagerSubscriptionUpdate>(SecretsManagerSeatLimitReached),

            {
                PasswordManagerUpdatedSeatTotal: var passwordManagerUpdatedSeatTotal,
                UpdatedSeatTotal: var secretsManagerUpdatedSeatTotal
            } when passwordManagerUpdatedSeatTotal < secretsManagerUpdatedSeatTotal =>
                new Invalid<SecretsManagerSubscriptionUpdate>(SecretsManagerCannotExceedPasswordManager),

            _ => new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate)
        };
}
