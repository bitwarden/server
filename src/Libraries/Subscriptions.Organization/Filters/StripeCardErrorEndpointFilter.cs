using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Stripe;

namespace Bit.Subscriptions.Organization.Filters;

/// <summary>
/// Turns a declined card into a bad request carrying Stripe's message. The group's basic exception handling does
/// not know about Stripe and would answer a declined card with a 500, so this filter runs inside it and rethrows
/// the card error as a <see cref="BadRequestException"/> for it to map.
/// </summary>
internal sealed class StripeCardErrorEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Type == StripeConstants.ErrorTypes.CardError)
        {
            throw new BadRequestException(stripeException.StripeError.Message);
        }
    }
}
