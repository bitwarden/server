using Bit.Core.Billing.Constants;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Commands;

using static StripeConstants;

public abstract class BillingCommand<T>(
    ILogger<T> logger)
{
    protected string CommandName => GetType().Name;

    protected async Task<BillingCommandResult<R>> HandleAsync<R>(Func<Task<BillingCommandResult<R>>> function)
    {
        try
        {
            return await function();
        }
        catch (StripeException stripeException) when (ErrorCodes.Get().Contains(stripeException.StripeError.Code))
        {
            return stripeException.StripeError.Code switch
            {
                ErrorCodes.CustomerTaxLocationInvalid =>
                    new BadRequest("Your location wasn't recognized. Please ensure your country and postal code are valid and try again."),

                ErrorCodes.PaymentMethodMicroDepositVerificationAttemptsExceeded =>
                    new BadRequest("You have exceeded the number of allowed verification attempts. Please contact support for assistance."),

                ErrorCodes.PaymentMethodMicroDepositVerificationDescriptorCodeMismatch =>
                    new BadRequest("The verification code you provided does not match the one sent to your bank account. Please try again."),

                ErrorCodes.PaymentMethodMicroDepositVerificationTimeout =>
                    new BadRequest("Your bank account was not verified within the required time period. Please contact support for assistance."),

                ErrorCodes.TaxIdInvalid =>
                    new BadRequest("The tax ID number you provided was invalid. Please try again or contact support for assistance."),

                _ => new Unhandled(stripeException)
            };
        }
        catch (StripeException stripeException)
        {
            logger.LogError(stripeException,
                "{Command}: An error occurred while communicating with Stripe | Code = {Code}", CommandName,
                stripeException.StripeError.Code);
            return new Unhandled(stripeException);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Command}: An unknown error occurred during execution", CommandName);
            return new Unhandled(exception);
        }
    }
}
