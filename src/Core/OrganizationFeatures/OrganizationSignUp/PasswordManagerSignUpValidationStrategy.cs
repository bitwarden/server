using Bit.Core.Services;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;

namespace Bit.Core.OrganizationFeatures.OrganizationSignUp;

public class PasswordManagerSignUpValidationStrategy : IOrganizationSignUpValidationStrategy
{
    public void Validate(Plan plan, OrganizationUpgrade upgrade)
    { 
        if (!plan.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0 )
        {
            throw new BadRequestException("Plan does not allow additional storage.");
        }

        if (upgrade.AdditionalStorageGb < 0)
        {
            throw new BadRequestException("You can't subtract storage!");
        }

        if (plan.BaseSeats + upgrade.AdditionalSeats <= 0)
        {
            throw new BadRequestException("You do not have any seats!");
        }

        if (upgrade.AdditionalSeats < 0)
        {
            throw new BadRequestException("You can't subtract seats!");
        }

        switch (plan.HasAdditionalSeatsOption)
        {
            case false when upgrade.AdditionalSeats > 0:
                throw new BadRequestException("Plan does not allow additional users.");
            case true when plan.MaxAdditionalSeats.HasValue &&
                           upgrade.AdditionalSeats > plan.MaxAdditionalSeats.Value:
                throw new BadRequestException($"Selected plan allows a maximum of " +
                                              $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }
}
