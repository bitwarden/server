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

    /// <summary>
    /// Chains the execution of another asynchronous validation step to the result of an initial validation.
    /// </summary>
    /// <param name="inputTask">The task representing the initial <see cref="ValidationResult{T}"/>.</param>
    /// <param name="next">A function that defines the next asynchronous validation step to execute if the initial validation is valid.</param>
    /// <typeparam name="T">The type of the request being validated.</typeparam>
    /// <returns>A task representing the result of the chained validation step.</returns>
    public static async Task<ValidationResult<T>> ThenAsync<T>(
        this Task<ValidationResult<T>> inputTask,
        Func<T, Task<ValidationResult<T>>> next)
    {
        var input = await inputTask;
        if (input.IsError) return Invalid(input.Request, input.AsError);
        return await next(input.Request);
    }
}
