using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
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
        Guid invitingUserId,
        Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(invitingUserId);
        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(true);

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerSeatLimitHasBeenReachedError.Code, organization.Seats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_WhenInviterCannotManageBilling_ShouldTellThemToContactTheOwner(
        Guid invitingUserId,
        Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(invitingUserId);
        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(false);

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerSeatLimitHasBeenReachedNoBillingAccessError.Code, organization.Seats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }

    /// <summary>
    /// SCIM and the Public API invite without an authenticated member, so there is nobody who could raise the seat
    /// limit in place and <see cref="ICurrentContext.EditSubscription"/> cannot be consulted.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_WithoutAnAuthenticatedUser_ShouldTellThemToContactTheOwner(
        Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(
            string.Format(PasswordManagerSeatLimitHasBeenReachedNoBillingAccessError.Code, organization.Seats),
            (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
        await sutProvider.GetDependency<ICurrentContext>().DidNotReceive().EditSubscription(Arg.Any<Guid>());
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
