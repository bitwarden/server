using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.Utilities.v2.Validation;

/// <summary>
/// Represents the result of validating a request.
/// This is for use within the Core layer, e.g. validating a command request.
/// </summary>
/// <param name="request">The request that has been validated.</param>
/// <param name="error">A <see cref="OneOf{Error, None}"/> type that contains an Error if validation failed.</param>
/// <typeparam name="TRequest">The request type.</typeparam>
public class ValidationResult<TRequest>(TRequest request, OneOf<Error, None> error) : OneOfBase<Error, None>(error)
{
    public TRequest Request { get; } = request;

    public bool IsError => IsT0;
    public bool IsValid => IsT1;
    public Error AsError => AsT0;
}

public static class ValidationResultHelpers
{
    /// <summary>
    /// Creates a successful <see cref="ValidationResult{TRequest}"/> with no error set.
    /// </summary>
    public static ValidationResult<T> Valid<T>(T request) => new(request, new None());
    /// <summary>
    /// Creates a failed <see cref="ValidationResult{TRequest}"/> with the specified error.
    /// </summary>
    public static ValidationResult<T> Invalid<T>(T request, Error error) => new(request, error);

    /// <summary>
    /// Extracts successfully validated requests from a sequence of <see cref="ValidationResult{TRequest}"/>.
    /// </summary>
    public static List<T> ValidRequests<T>(this IEnumerable<ValidationResult<T>> results) =>
        results
            .Where(r => r.IsValid)
            .Select(r => r.Request)
            .ToList();
}
