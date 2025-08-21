using OneOf.Types;
using OneOf;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

/// <summary>
/// Represents the result of validating a request.
/// This is for use within the Core layer, e.g. validating a command request.
/// </summary>
/// <param name="Request">The request that has been validated.</param>
/// <param name="Error">A <see cref="OneOf{Error, None}"/> type that contains an Error if validation failed.</param>
/// <typeparam name="TRequest">The request type.</typeparam>
public record ValidationResult<TRequest>(TRequest Request, OneOf<Error, None> Error);

public static class ValidationResultHelpers
{
    public static ValidationResult<T> Valid<T>(T request) => new (request, new None());
    public static ValidationResult<T> Invalid<T>(T request, Error error) => new (request, error);

    public static List<T> ValidResults<T>(this IEnumerable<ValidationResult<T>> results) =>
        results
            .Where(r => r.Error.IsT1)
            .Select(r => r.Request)
            .ToList();
}
