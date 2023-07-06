using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscription;

public class SecretsManagerPlanValidation : ISecretsManagerPlanValidation
{
    public void ValidateSecretsManagerPlan(Plan plan, Organization signup, int additionalSeats,
        int additionalServiceAccounts)
    {
        if (plan is not { LegacyYear: null })
        {
            throw new BadRequestException($"Invalid Secrets Manager plan selected.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException($"Secrets Manager Plan not found.");
        }

        if (plan.BaseSeats + additionalSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Secrets Manager seats!");
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract Secrets Manager seats!");
        }

        if (!plan.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Service Accounts.");
        }

        if (additionalSeats > signup.Seats)
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (additionalServiceAccounts < 0)
        {
            throw new BadRequestException("You can't subtract Service Accounts!");
        }

        switch (plan.HasAdditionalSeatsOption)
        {
            case false when additionalSeats > 0:
                throw new BadRequestException("Plan does not allow additional users.");
            case true when plan.MaxAdditionalSeats.HasValue &&
                           additionalSeats > plan.MaxAdditionalSeats.Value:
                throw new BadRequestException($"Selected plan allows a maximum of " +
                                              $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }
}
