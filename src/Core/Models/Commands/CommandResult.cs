namespace Bit.Core.Models.Commands;

public class CommandResult(IEnumerable<string> errors)
{
    public CommandResult(string error) : this([error]) { }

    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();
    public CommandResult() : this(Array.Empty<string>()) { }
}

public abstract class CommandResult<T> : CommandResult
{

    public T Value { get; set; }
}

public class Success<T> : CommandResult<T>
{
    public Success(T value) => Value = value;
}

public class Failure<T> : CommandResult<T>
{
    public Failure(string error) => ErrorMessages.Add(error);

    public string ErrorMessage => string.Join(" ", ErrorMessages);
}
