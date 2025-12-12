using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Payments;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Test.Billing.Mocks.Plans;
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

        var result = InviteUserPaymentValidation.Validate(new PaymentsSubscription
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Active,
            ProductTierType = new InviteOrganization(organization, new FreePlan()).Plan.ProductTier
        });

        Assert.IsType<Valid<PaymentsSubscription>>(result);
    }

    [Fact]
    public void Validate_WhenSubscriptionIsCanceled_ReturnsInvalidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentsSubscription
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Canceled,
            ProductTierType = ProductTierType.Enterprise
        });

        Assert.IsType<Invalid<PaymentsSubscription>>(result);
        Assert.Equal(PaymentCancelledSubscriptionError.Code, (result as Invalid<PaymentsSubscription>)!.Error.Message);
    }

    [Fact]
    public void Validate_WhenSubscriptionIsActive_ReturnsValidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentsSubscription
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Active,
            ProductTierType = ProductTierType.Enterprise
        });

        Assert.IsType<Valid<PaymentsSubscription>>(result);
    }
}
