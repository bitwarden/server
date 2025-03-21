using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;

public static class SecretsManagerInviteUserValidation
{
    public static ValidationResult<SecretsManagerSubscriptionUpdate> Validate(
        SecretsManagerSubscriptionUpdate subscriptionUpdate) =>
        subscriptionUpdate switch
        {
            { UseSecretsManger: false, AdditionalSeats: > 0 } =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    new OrganizationNoSecretsManagerError(subscriptionUpdate)),

            { UseSecretsManger: false, AdditionalSeats: 0 } or { UseSecretsManger: true, Seats: null } =>
                new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate),

            { UseSecretsManger: true, SecretsManagerPlan.HasAdditionalSeatsOption: false } =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    new SecretsManagerAdditionalSeatLimitReachedError(subscriptionUpdate)),

            { UseSecretsManger: true, SecretsManagerPlan.MaxAdditionalSeats: var planMaxSeats }
                when planMaxSeats < subscriptionUpdate.AdditionalSeats =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    new SecretsManagerAdditionalSeatLimitReachedError(subscriptionUpdate)),

            { UseSecretsManger: true, UpdatedSeatTotal: var updateSeatTotal, MaxAutoScaleSeats: var maxAutoScaleSeats }
                when updateSeatTotal > maxAutoScaleSeats =>
                new Invalid<SecretsManagerSubscriptionUpdate>(
                    new SecretsManagerSeatLimitReachedError(subscriptionUpdate)),

            {
                UseSecretsManger: true,
                PasswordManagerUpdatedSeatTotal: var passwordManagerUpdatedSeatTotal,
                UpdatedSeatTotal: var secretsManagerUpdatedSeatTotal
            }
                when passwordManagerUpdatedSeatTotal < secretsManagerUpdatedSeatTotal =>
                    new Invalid<SecretsManagerSubscriptionUpdate>(
                        new SecretsManagerCannotExceedPasswordManagerError(subscriptionUpdate)),

            _ => new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate)
        };
}
