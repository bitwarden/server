using Bit.Core.Billing.Commands;
using Bit.Core.Exceptions;

namespace Bit.Subscriptions.Organization.Commands;

internal static class BillingCommandResultExtensions
{
    // The seat change step and the subscription update still report failures as a BillingCommandResult. The
    // endpoint exception filter has no case for BillingException, so surface each failure as the exception that
    // maps to its HTTP status.
    public static T Unwrap<T>(this BillingCommandResult<T> result) => result.Match(
        value => value,
        badRequest => throw new BadRequestException(badRequest.Response),
        conflict => throw new ConflictException(conflict.Response),
        unhandled => throw unhandled.Exception ?? new InvalidOperationException(unhandled.Response));
}
