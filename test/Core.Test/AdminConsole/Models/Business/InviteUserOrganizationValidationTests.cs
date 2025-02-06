using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Business;

public class InviteUserOrganizationValidationTests
{

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationIsFreeTier_ShouldReturnValidResponse(Organization organization)
    {
        organization.PlanType = PlanType.Free;
        var organizationDto = OrganizationDto.FromOrganization(organization);

        var result = InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Valid<OrganizationDto>>(result);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization)
    {
        organization.PlanType = PlanType.EnterpriseMonthly;
        organization.GatewayCustomerId = string.Empty;

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var result = InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoPaymentMethodFoundError, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public void Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization)
    {
        organization.PlanType = PlanType.EnterpriseMonthly;
        organization.GatewaySubscriptionId = string.Empty;

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var result = InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoSubscriptionFoundError, result.ErrorMessageString);
    }
}
