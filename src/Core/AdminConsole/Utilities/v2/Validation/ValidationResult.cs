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

    /// <summary>
    /// Invokes <paramref name="error"/> when validation failed, or <paramref name="valid"/> with the
    /// validated <see cref="Request"/> when validation succeeded.
    /// </summary>
    public void SwitchResult(Action<Error> error, Action<TRequest> valid) =>
        Switch(error, _ => valid(Request));

    /// <summary>
    /// Returns the result of <paramref name="error"/> when validation failed, or the result of
    /// <paramref name="valid"/> invoked with the validated <see cref="Request"/> when validation succeeded.
    /// </summary>
    public TResult MatchResult<TResult>(Func<Error, TResult> error, Func<TRequest, TResult> valid) =>
        Match(error, _ => valid(Request));
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
