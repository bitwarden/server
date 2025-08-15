using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Commands;

using static StripeConstants;

public abstract class BaseBillingCommand<T>(
    ILogger<T> logger)
{
    protected string CommandName => GetType().Name;

    /// <summary>
    /// Override this property to set a client-facing conflict response in the case a <see cref="ConflictException"/> is thrown
    /// during the command's execution.
    /// </summary>
    protected virtual Conflict? DefaultConflict => null;

    /// <summary>
    /// Executes the provided function within a predefined execution context, handling any exceptions that occur during the process.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful result expected from the provided function.</typeparam>
    /// <param name="function">A function that performs an operation and returns a <see cref="BillingCommandResult{TSuccess}"/>.</param>
    /// <returns>A task that represents the operation. The result provides a <see cref="BillingCommandResult{TSuccess}"/> which may indicate success or an error outcome.</returns>
    protected async Task<BillingCommandResult<TSuccess>> HandleAsync<TSuccess>(
        Func<Task<BillingCommandResult<TSuccess>>> function)
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
                    new BadRequest(
                        "Your location wasn't recognized. Please ensure your country and postal code are valid and try again."),

                ErrorCodes.PaymentMethodMicroDepositVerificationAttemptsExceeded =>
                    new BadRequest(
                        "You have exceeded the number of allowed verification attempts. Please contact support for assistance."),

                ErrorCodes.PaymentMethodMicroDepositVerificationDescriptorCodeMismatch =>
                    new BadRequest(
                        "The verification code you provided does not match the one sent to your bank account. Please try again."),

                ErrorCodes.PaymentMethodMicroDepositVerificationTimeout =>
                    new BadRequest(
                        "Your bank account was not verified within the required time period. Please contact support for assistance."),

                ErrorCodes.TaxIdInvalid =>
                    new BadRequest(
                        "The tax ID number you provided was invalid. Please try again or contact support for assistance."),

                _ => new Unhandled(stripeException)
            };
        }
        catch (ConflictException conflictException)
        {
            logger.LogError("{Command}: {Message}", CommandName, conflictException.Message);
            return DefaultConflict != null ?
                DefaultConflict :
                new Unhandled(conflictException);
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
