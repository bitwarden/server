using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;

namespace Bit.Core.OrganizationFeatures.OrganizationSignUp;

public class SecretsManagerSignUpValidationStrategy : IOrganizationSignUpValidationStrategy
{
    public void Validate(Plan plan, OrganizationUpgrade upgrade)
    { 
        if (!plan.HasAdditionalServiceAccountOption && upgrade.AdditionalServiceAccount > 0 )
        {
            throw new BadRequestException("Plan does not allow additional service account.");
        }

        if (upgrade.AdditionalServiceAccount < 0)
        {
            throw new BadRequestException("You can't subtract service account!");
        }

        if (plan.BaseSeats + upgrade.AdditionalSmSeats <= 0)
        {
            throw new BadRequestException("You do not have any seats!");
        }

        if (upgrade.AdditionalSmSeats < 0)
        {
            throw new BadRequestException("You can't subtract secrets manager seats!");
        }

        if (!plan.HasAdditionalSeatsOption && upgrade.AdditionalSmSeats > 0)
        {
            throw new BadRequestException("Plan does not allow additional users.");
        }

        if (plan.HasAdditionalSeatsOption && plan.MaxAdditionalSeats.HasValue &&
            upgrade.AdditionalSmSeats > plan.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Selected plan allows a maximum of " +
                                          $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }
}
