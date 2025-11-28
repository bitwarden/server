using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

[SutProviderCustomize]
public class InviteUserOrganizationValidationTests
{
    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationIsFreeTier_ShouldReturnValidResponse(Organization organization, SutProvider<InviteUsersOrganizationValidator> sutProvider)
    {
        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var result = await sutProvider.Sut.ValidateAsync(inviteOrganization);

        Assert.IsType<Valid<InviteOrganization>>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization, SutProvider<InviteUsersOrganizationValidator> sutProvider)
    {
        organization.GatewayCustomerId = string.Empty;
        organization.Seats = 3;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var result = await sutProvider.Sut.ValidateAsync(inviteOrganization);

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoPaymentMethodFoundError.Code, (result as Invalid<InviteOrganization>)!.Error.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization, SutProvider<InviteUsersOrganizationValidator> sutProvider)
    {
        organization.GatewaySubscriptionId = string.Empty;
        organization.Seats = 3;
        organization.MaxAutoscaleSeats = 4;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var result = await sutProvider.Sut.ValidateAsync(inviteOrganization);

        Assert.IsType<Invalid<InviteOrganization>>(result);
        Assert.Equal(OrganizationNoSubscriptionFoundError.Code, (result as Invalid<InviteOrganization>)!.Error.Message);
    }
}
