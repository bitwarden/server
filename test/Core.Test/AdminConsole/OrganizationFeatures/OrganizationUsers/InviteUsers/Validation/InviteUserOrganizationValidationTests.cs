using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
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
        var result = InviteUserOrganizationValidator.Validate(new InviteOrganization(organization, new FreePlan()));

        Assert.IsType<Valid<InviteOrganization>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization)
    {
        organization.GatewayCustomerId = string.Empty;

        var result = InviteUserOrganizationValidator.Validate(new InviteOrganization(organization, new FreePlan()));

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoPaymentMethodFoundError.Code, (result as Invalid<InviteOrganization>)!.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization)
    {
        organization.GatewaySubscriptionId = string.Empty;

        var result = InviteUserOrganizationValidator.Validate(new InviteOrganization(organization, new FreePlan()));

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoSubscriptionFoundError.Code, (result as Invalid<InviteOrganization>)!.ErrorMessageString);
    }
}
