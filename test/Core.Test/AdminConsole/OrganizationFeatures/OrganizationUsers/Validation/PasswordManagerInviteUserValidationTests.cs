using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public class PasswordManagerInviteUserValidationTests
{

    [Theory]
    [BitAutoData]
    public void Validate_OrganizationDoesNotHaveSeatsLimit_ShouldReturnValidResult(Organization organization)
    {
        organization.Seats = null;

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NumberOfSeatsToAddMatchesSeatsAvailable_ShouldReturnValidResult(Organization organization)
    {
        organization.Seats = 8;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 4;

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_ShouldBeInvalidWithSeatLimitMessage(Organization organization)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var subscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.SeatLimitHasBeenReachedError, result.ErrorMessageString);
    }

}
