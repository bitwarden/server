using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public class InviteUserOrganizationValidationTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationIsFreeTier_ShouldReturnValidResponse(Organization organization)
    {
        var result = InvitingUserOrganizationValidation.Validate(OrganizationDto.FromOrganization(organization));

        Assert.IsType<Valid<OrganizationDto>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization)
    {
        organization.GatewayCustomerId = string.Empty;

        var result = InvitingUserOrganizationValidation.Validate(OrganizationDto.FromOrganization(organization));

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoPaymentMethodFoundError, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization)
    {
        organization.GatewaySubscriptionId = string.Empty;

        var result = InvitingUserOrganizationValidation.Validate(OrganizationDto.FromOrganization(organization));

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoSubscriptionFoundError, result.ErrorMessageString);
    }
}
