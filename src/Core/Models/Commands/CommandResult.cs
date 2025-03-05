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

public class FailureCommandResult : CommandResult
{
    public FailureCommandResult(IEnumerable<string> errorMessages) : base(errorMessages)
    {

    }
    public FailureCommandResult(string errorMessage) : base(errorMessage)
    {

    }
}

public class SuccessCommandResult : CommandResult
{
}

public abstract class CommandResult<T>
{

}

public class SuccessCommandResult<T> : CommandResult<T>
{
    public T? Data { get; init; }

    public SuccessCommandResult(T data)
    {
        Data = data;
    }
}

public class FailureCommandResult<T> : CommandResult<T>
{
    public IEnumerable<string> ErrorMessages { get; init; }

    public FailureCommandResult(IEnumerable<string> errorMessage)
    {
        ErrorMessages = errorMessage;
    }

    public FailureCommandResult(string errorMessage)
    {
        ErrorMessages = new[] { errorMessage };
    }
}


