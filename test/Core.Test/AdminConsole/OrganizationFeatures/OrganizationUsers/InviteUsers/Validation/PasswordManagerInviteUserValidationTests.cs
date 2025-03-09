using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;


public class PasswordManagerInviteUserValidationTests
{
    [Theory]
    [BitAutoData]
    public void Validate_OrganizationDoesNotHaveSeatsLimit_ShouldReturnValidResult(Organization organization)
    {
        organization.Seats = null;

        var organizationDto = new InviteOrganization(organization);

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NumberOfSeatsToAddMatchesSeatsAvailable_ShouldReturnValidResult(Organization organization)
    {
        organization.Seats = 8;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 4;

        var organizationDto = new InviteOrganization(organization);

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_ShouldBeInvalidWithSeatLimitMessage(Organization organization)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        var organizationDto = new InviteOrganization(organization);

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.SeatLimitHasBeenReachedError, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_GivenThePlanDoesNotAllowAdditionalSeats_ShouldBeInvalidMessageOfPlanNotAllowingSeats(Organization organization)
    {
        organization.Seats = 8;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 4;
        organization.PlanType = PlanType.Free;

        var organizationDto = new InviteOrganization(organization);

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = PasswordManagerInviteUserValidation.Validate(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.PlanDoesNotAllowAdditionalSeats, result.ErrorMessageString);
    }
}
