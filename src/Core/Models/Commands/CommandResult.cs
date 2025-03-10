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

public abstract class CommandResult<T>
{

}

public class Success<T>(T data) : CommandResult<T>
{
    public T? Data { get; init; } = data;
}

public class Failure<T>(IEnumerable<string> errorMessage) : CommandResult<T>
{
    public IEnumerable<string> ErrorMessages { get; init; } = errorMessage;

    public Failure(string errorMessage) : this(new[] { errorMessage })
    {
    }
}

