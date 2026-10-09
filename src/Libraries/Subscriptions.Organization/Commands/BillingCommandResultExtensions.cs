using Bit.Core.Billing.Commands;
using Bit.Core.Exceptions;

namespace Bit.Subscriptions.Organization.Commands;

internal static class BillingCommandResultExtensions
{
    /// <summary>
    /// Returns the value of a successful result, or throws the exception that maps to the failure's HTTP status.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    /// <param name="result">The result of a billing command.</param>
    /// <returns>The value of the successful result.</returns>
    /// <exception cref="BadRequestException">Thrown when the result is a <see cref="BadRequest"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the result is a <see cref="Conflict"/>.</exception>
    /// <exception cref="Exception">
    /// Thrown when the result is <see cref="Unhandled"/>: the underlying exception if there is one, otherwise an
    /// <see cref="InvalidOperationException"/> carrying the response message.
    /// </exception>
    public static T Unwrap<T>(this BillingCommandResult<T> result) => result.Match(
        value => value,
        badRequest => throw new BadRequestException(badRequest.Response),
        conflict => throw new ConflictException(conflict.Response),
        unhandled => throw unhandled.Exception ?? new InvalidOperationException(unhandled.Response));
}
