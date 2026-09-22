using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;


[SutProviderCustomize]
public class InviteUsersPasswordManagerValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task Validate_OrganizationDoesNotHaveSeatsLimit_ShouldReturnValidResult(Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = null;

        var organizationDto = new InviteOrganization(organization, new FreePlan());

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, 0, 0);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_NumberOfSeatsToAddMatchesSeatsAvailable_ShouldReturnValidResult(Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 8;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 4;

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Valid<PasswordManagerSubscriptionUpdate>>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_WhenInviterCanManageBilling_ShouldTellThemToIncreaseTheSeatLimit(
        Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        // Seats and MaxAutoscaleSeats deliberately differ so the assertion pins which one the message reports.
        // Autoscaling to 11 overshoots the cap of 10, so the limit the member hit is 10, not their current 5.
        organization.Seats = 5;
        organization.MaxAutoscaleSeats = 10;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 5;
        var additionalSeats = 6;

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers,
            additionalSeats, canManageBilling: true);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerSeatLimitHasBeenReachedError.Code, organization.MaxAutoscaleSeats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }

    /// <summary>
    /// The flag defaults to false, which also covers the callers that invite without an authenticated member (SCIM
    /// and the Public API), where nobody could raise the seat limit in place.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_WhenInviterCannotManageBilling_ShouldTellThemToContactTheOwner(
        Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        // Seats and MaxAutoscaleSeats deliberately differ so the assertion pins which one the message reports.
        organization.Seats = 5;
        organization.MaxAutoscaleSeats = 10;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 5;
        var additionalSeats = 6;

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerSeatLimitHasBeenReachedNoBillingAccessError.Code, organization.MaxAutoscaleSeats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_GivenThePlanDoesNotAllowAdditionalSeats_ShouldBeInvalidMessageOfPlanNotAllowingSeats(Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 9;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 4;
        organization.PlanType = PlanType.Free;

        var organizationDto = new InviteOrganization(organization, new FreePlan());

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerPlanDoesNotAllowAdditionalSeatsError.Code, organization.Seats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }
}
