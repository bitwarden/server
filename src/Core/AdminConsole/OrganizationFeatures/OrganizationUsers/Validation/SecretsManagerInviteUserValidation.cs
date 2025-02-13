namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public class SecretsManagerInviteUserValidation
{
    // Do we need to check if they are attempting to subtract seats? (no I don't think so because this is for inviting a User)
    public static ValidationResult<SecretsManagerSubscriptionUpdate> Validate(SecretsManagerSubscriptionUpdate subscriptionUpdate)
    {
        if (subscriptionUpdate.UseSecretsManger)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(InviteUserValidationErrorMessages.OrganizationNoSecretsManager);
        }

        if (subscriptionUpdate.Seats == null)
        {
            return new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate); // no need to adjust seats...continue on
        }

        // if (update.Autoscaling && update.SmSeats.Value < organization.SmSeats.Value)
        // {
        //     throw new BadRequestException("Cannot use autoscaling to subtract seats.");
        // }

        // Might need to check plan

        // Check plan maximum seats
        // if (!plan.SecretsManager.HasAdditionalSeatsOption ||
        //     (plan.SecretsManager.MaxAdditionalSeats.HasValue && update.SmSeatsExcludingBase > plan.SecretsManager.MaxAdditionalSeats.Value))
        // {
        //     var planMaxSeats = plan.SecretsManager.BaseSeats + plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault();
        //     throw new BadRequestException($"You have reached the maximum number of Secrets Manager seats ({planMaxSeats}) for this plan.");
        // }

        // Check autoscale maximum seats
        if (subscriptionUpdate.UpdatedSeatTotal is not null && subscriptionUpdate.MaxAutoScaleSeats is not null &&
            subscriptionUpdate.UpdatedSeatTotal > subscriptionUpdate.MaxAutoScaleSeats)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(InviteUserValidationErrorMessages
                .SecretsManagerSeatLimitReached);
        }

        // if (update.MaxAutoscaleSmSeats.HasValue && update.SmSeats.Value > update.MaxAutoscaleSmSeats.Value)
        // {
        //     var message = update.Autoscaling
        //         ? "Secrets Manager seat limit has been reached."
        //         : "Cannot set max seat autoscaling below seat count.";
        //     throw new BadRequestException(message);
        // }

        // Inviting a user... this shouldn't matter
        //
        // Check minimum seats included with plan
        // if (plan.SecretsManager.BaseSeats > update.SmSeats.Value)
        // {
        //     throw new BadRequestException($"Plan has a minimum of {plan.SecretsManager.BaseSeats} Secrets Manager  seats.");
        // }

        // Check minimum seats required by business logic
        // if (update.SmSeats.Value <= 0)
        // {
        //     throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        // }

        // Check minimum seats currently in use by the organization
        // if (organization.SmSeats.Value > update.SmSeats.Value)
        // {
        //     var occupiedSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
        //     if (occupiedSeats > update.SmSeats.Value)
        //     {
        //         throw new BadRequestException($"{occupiedSeats} users are currently occupying Secrets Manager seats. " +
        //                                       "You cannot decrease your subscription below your current occupied seat count.");
        //     }
        // }

        // Check that SM seats aren't greater than password manager seats
        if (subscriptionUpdate.PasswordManagerSubscriptionUpdate.UpdatedSeatTotal < subscriptionUpdate.UpdatedSeatTotal)
        {
            return new Invalid<SecretsManagerSubscriptionUpdate>(InviteUserValidationErrorMessages.SecretsManagerCannotExceedPasswordManager);
        }

        return new Valid<SecretsManagerSubscriptionUpdate>(subscriptionUpdate);
    }
}
