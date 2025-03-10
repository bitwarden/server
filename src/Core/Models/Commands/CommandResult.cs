﻿#nullable enable

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

    public string ErrorMessage => string.Join(" ", ErrorMessages);

    public Failure(string error) : this([error]) { }
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

public abstract record Error<T>(string Message, T ErroredValue);

public record RecordNotFoundError<T>(string Message, T ErroredValue) : Error<T>(Message, ErroredValue)
{
    public const string Code = "Record Not Found";

    public RecordNotFoundError(T ErroredValue) : this(Code, ErroredValue)
    {

    }
}

public record InsufficientPermissionsError<T>(string Message, T ErroredValue) : Error<T>(Message, ErroredValue)
{
    public const string Code = "Insufficient Permissions";

    public InsufficientPermissionsError(T ErroredValue) : this(Code, ErroredValue)
    {

    }
}

public record InvalidRequestError<T>(string Message, T ErroredValue) : Error<T>(Message, ErroredValue)
{
    public const string Code = "Invalid Request";

    public InvalidRequestError(T ErroredValue) : this(Code, ErroredValue)
    {

    }
}
