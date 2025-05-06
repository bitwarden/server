#nullable enable

using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.Utilities.Commands;

public abstract class CommandResult<T>;

public class Success<T>(T value) : CommandResult<T>
{
    public T Value { get; } = value;
}

public class Failure<T>(Error<T> error) : CommandResult<T>
{
    public Error<T> Error { get; } = error;
}

public class Partial<T>(IEnumerable<T> successfulItems, IEnumerable<Error<T>> failedItems)
    : CommandResult<T>
{
    public IEnumerable<T> Successes { get; } = successfulItems;
    public IEnumerable<Error<T>> Failures { get; } = failedItems;
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
        new Failure<B>(invalidResult.Error.ToError(mappingFunction(invalidResult.Error.ErroredValue)));
}

[Obsolete("Use CommandResult<T> instead. This will be removed once old code is updated.")]
public class CommandResult(IEnumerable<string> errors)
{
    public CommandResult(string error) : this([error]) { }

    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();
    public CommandResult() : this(Array.Empty<string>()) { }
}
