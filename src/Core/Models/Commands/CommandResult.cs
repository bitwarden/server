#nullable enable

namespace Bit.Core.Models.Commands;

public class CommandResult(IEnumerable<string> errors)
{
    public CommandResult(string error) : this([error]) { }

    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();

    public CommandResult() : this(Array.Empty<string>()) { }
}

public abstract class CommandResult<T>
{
    public T? Data { get; init; }
    public IEnumerable<string> Errors { get; init; } = [];
}

public class SuccessCommandResult<T> : CommandResult<T>
{
    public SuccessCommandResult(T data)
    {
        Data = data;
    }
}

public class FailureCommandResult<T> : CommandResult<T>
{
    public FailureCommandResult(IEnumerable<string> errorMessage)
    {
        Errors = errorMessage;
    }

    public FailureCommandResult(string errorMessage)
    {
        Errors = new[] { errorMessage };
    }
}


