using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Business;

public class InviteUserOrganizationValidationTests
{

    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationIsFreeTier_ShouldReturnValidResponse(Organization organization)
    {
        var paymentService = Substitute.For<IPaymentService>();

        organization.PlanType = PlanType.Free;
        var organizationDto = new OrganizationValidationDto
        {
            Organization = OrganizationDto.FromOrganization(organization),
            PaymentService = paymentService
        };

        var result = await InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Valid<OrganizationDto>>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationDoesNotHavePaymentMethod_ShouldReturnInvalidResponseWithPaymentMethodMessage(
        Organization organization)
    {
        var paymentService = Substitute.For<IPaymentService>();

        organization.GatewayCustomerId = string.Empty;
        organization.PlanType = PlanType.EnterpriseMonthly;
        var organizationDto = new OrganizationValidationDto
        {
            Organization = OrganizationDto.FromOrganization(organization),
            PaymentService = paymentService
        };

        var result = await InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoPaymentMethodFoundError, result.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public async Task Validate_WhenOrganizationDoesNotHaveSubscription_ShouldReturnInvalidResponseWithSubscriptionMessage(
        Organization organization)
    {
        organization.PlanType = PlanType.EnterpriseMonthly;
        organization.GatewaySubscriptionId = string.Empty;
        var paymentService = Substitute.For<IPaymentService>();

        var organizationDto = new OrganizationValidationDto
        {
            Organization = OrganizationDto.FromOrganization(organization),
            PaymentService = paymentService
        };

        var result = await InvitingUserOrganizationValidation.Validate(organizationDto);

        Assert.IsType<Invalid<OrganizationDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.NoSubscriptionFoundError, result.ErrorMessageString);
    }
}
