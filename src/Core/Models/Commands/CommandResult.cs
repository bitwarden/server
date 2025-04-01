#nullable enable

using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.Models.Commands;

public class CommandResult(IEnumerable<string> errors)
{
    public CommandResult(string error) : this([error]) { }

    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();
    public CommandResult() : this(Array.Empty<string>()) { }
}

public class Failure : CommandResult
{
    protected Failure(IEnumerable<string> errorMessages) : base(errorMessages)
    {

    }
    public Failure(string errorMessage) : base(errorMessage)
    {

    }
}

public class Success : CommandResult
{
}

public abstract class CommandResult<T>;

public class Success<T>(T value) : CommandResult<T>
{
    public T Value { get; } = value;
}

public class Failure<T>(IEnumerable<string> errorMessages) : CommandResult<T>
{
    public List<string> ErrorMessages { get; } = errorMessages.ToList();
    public Error<T>[] Errors { get; set; } = [];

    public string ErrorMessage => string.Join(" ", ErrorMessages);

    public Failure(string error) : this([error])
    {
    }

    public Failure(IEnumerable<Error<T>> errors) : this(errors.Select(e => e.Message))
    {
        Errors = errors.ToArray();
    }

    public Failure(Error<T> error) : this([error.Message])
    {
        Errors = [error];
    }
}

public class Partial<T> : CommandResult<T>
{
    public T[] Successes { get; set; } = [];
    public Error<T>[] Failures { get; set; } = [];

    public Partial(IEnumerable<T> successfulItems, IEnumerable<Error<T>> failedItems)
    {
        Successes = successfulItems.ToArray();
        Failures = failedItems.ToArray();
    }
}

public static class CommandResultExtensions
{
    /// <summary>
    /// This is to help map between the InvalidT ValidationResult and the FailureT CommandResult types.
    ///
    /// </summary>
    /// <param name="invalidResult">This is the invalid type from validating the object.</param>
    /// <param name="mappingFunction">This function will map between the two types for the inner ErrorT</param>
    /// <typeparam name="A">Invalid object's type</typeparam>
    /// <typeparam name="B">Failure object's type</typeparam>
    /// <returns></returns>
    public static CommandResult<B> MapToFailure<A, B>(this Invalid<A> invalidResult, Func<A, B> mappingFunction) =>
        new Failure<B>(invalidResult.Errors.Select(errorA => errorA.ToError(mappingFunction(errorA.ErroredValue))));
}
