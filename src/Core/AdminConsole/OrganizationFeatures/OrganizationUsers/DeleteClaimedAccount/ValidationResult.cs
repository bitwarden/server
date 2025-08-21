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
    /// <summary>
    /// Creates a successful <see cref="ValidationResult{TRequest}"/> with no error set.
    /// </summary>
    public static ValidationResult<T> Valid<T>(T request) => new (request, new None());
    /// <summary>
    /// Creates a failed <see cref="ValidationResult{TRequest}"/> with the specified error.
    /// </summary>
    public static ValidationResult<T> Invalid<T>(T request, Error error) => new (request, error);

    /// <summary>
    /// Extracts successfully validated results from a sequence of <see cref="ValidationResult{TRequest}"/>.
    /// </summary>
    public static List<T> ValidResults<T>(this IEnumerable<ValidationResult<T>> results) =>
        results
            .Where(r => r.Error.IsT1)
            .Select(r => r.Request)
            .ToList();
}
