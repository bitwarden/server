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
    public async Task Validate_NumberOfSeatsToAddIsGreaterThanMaxSeatsAllowed_ShouldBeInvalidWithSeatLimitMessage(Organization organization,
        SutProvider<InviteUsersPasswordManagerValidator> sutProvider)
    {
        organization.Seats = 4;
        organization.MaxAutoscaleSeats = 4;
        organization.PlanType = PlanType.EnterpriseAnnually;
        var seatsOccupiedByUsers = 4;
        var additionalSeats = 1;

        var organizationDto = new InviteOrganization(organization, new Enterprise2023Plan(isAnnual: true));

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, seatsOccupiedByUsers, additionalSeats);

        var result = await sutProvider.Sut.ValidateAsync(subscriptionUpdate);

        Assert.IsType<Invalid<PasswordManagerSubscriptionUpdate>>(result);
        Assert.Equal(PasswordManagerSeatLimitHasBeenReachedError.Code, (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
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
        Assert.Equal(PasswordManagerPlanDoesNotAllowAdditionalSeatsError.Code, (result as Invalid<PasswordManagerSubscriptionUpdate>)!.Error.Message);
    }
}
