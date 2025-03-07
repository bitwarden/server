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

public abstract class CommandResult<T> { }

public class Success<T>(T value) : CommandResult<T>
{
    public T? Value { get; } = value;
}

public class Failure<T>(IEnumerable<string> errorMessage) : CommandResult<T>
{
    public List<string> ErrorMessages { get; } = [];

    public string ErrorMessage => string.Join(" ", ErrorMessages);

    public Failure(string error) : this([error]) { }
}
