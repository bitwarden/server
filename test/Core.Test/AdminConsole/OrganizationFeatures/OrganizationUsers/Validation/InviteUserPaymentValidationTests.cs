using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;
using Bit.Core.Billing.Constants;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.Validation;

public class InviteUserPaymentValidationTests
{

    [Fact]
    public void Validate_WhenSubscriptionIsCanceled_ReturnsInvalidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentSubscriptionDto
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Canceled
        });

        Assert.IsType<Invalid<PaymentSubscriptionDto>>(result);
        Assert.Equal(InviteUserValidationErrorMessages.CancelledSubscriptionError, result.ErrorMessageString);
    }

    [Fact]
    public void Validate_WhenSubscriptionIsActive_ReturnsValidResponse()
    {
        var result = InviteUserPaymentValidation.Validate(new PaymentSubscriptionDto
        {
            SubscriptionStatus = StripeConstants.SubscriptionStatus.Active
        });

        Assert.IsType<Valid<PaymentSubscriptionDto>>(result);
    }
}
