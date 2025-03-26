using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public class InviteUserOrganizationValidationTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationIsFreeTier_ShouldReturnValidResponse(Organization organization)
    {
        var inviteOrganization = new InviteOrganization(organization, new FreePlan());
        var validSubscriptionUpdate = new Valid<PasswordManagerSubscriptionUpdate>(
                new PasswordManagerSubscriptionUpdate(inviteOrganization, 0, 0));

        var result = InviteUserOrganizationValidator.Validate(inviteOrganization, validSubscriptionUpdate);

        Assert.IsType<Valid<InviteOrganization>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization)
    {
        organization.GatewayCustomerId = string.Empty;
        organization.Seats = 3;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());
        var validSubscriptionUpdate = new Valid<PasswordManagerSubscriptionUpdate>(
            new PasswordManagerSubscriptionUpdate(inviteOrganization, 3, 1));

        var result = InviteUserOrganizationValidator.Validate(inviteOrganization, validSubscriptionUpdate);

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoPaymentMethodFoundError.Code, (result as Invalid<InviteOrganization>)!.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization)
    {
        organization.GatewaySubscriptionId = string.Empty;
        organization.Seats = 3;
        organization.MaxAutoscaleSeats = 4;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());
        var validSubscriptionUpdate = new Valid<PasswordManagerSubscriptionUpdate>(
            new PasswordManagerSubscriptionUpdate(inviteOrganization, 3, 1));

        var result = InviteUserOrganizationValidator.Validate(inviteOrganization, validSubscriptionUpdate);

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoSubscriptionFoundError.Code, (result as Invalid<InviteOrganization>)!.ErrorMessageString);
    }
}
