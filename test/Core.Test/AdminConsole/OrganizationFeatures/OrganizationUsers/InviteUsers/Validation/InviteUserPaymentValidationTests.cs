using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

public class InviteUserPaymentValidationTests
{
    [Theory]
    [BitAutoData]
    public void Validate_WhenPlanIsFree_ReturnsValidResponse(Organization organization)
    {
        organization.PlanType = PlanType.Free;

        var result = InviteUserPaymentValidation.Validate(new PaymentSubscriptionDto
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Active,
            ProductTierType = OrganizationDto.FromOrganization(organization).Plan.ProductTier
        });

        Assert.IsType<Valid<PaymentSubscriptionDto>>(result);
    }

    [Fact]
    public void Validate_WhenSubscriptionIsCanceled_ReturnsInvalidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentSubscriptionDto
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Canceled,
            ProductTierType = ProductTierType.Enterprise
        });

        Assert.IsType<Invalid<PaymentSubscriptionDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.CancelledSubscriptionError, result.ErrorMessageString);
    }

    [Fact]
    public void Validate_WhenSubscriptionIsActive_ReturnsValidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentSubscriptionDto
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Active,
            ProductTierType = ProductTierType.Enterprise
        });

        Assert.IsType<Valid<PaymentSubscriptionDto>>(result);
    }
}
