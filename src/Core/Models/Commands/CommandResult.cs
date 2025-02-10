namespace Bit.Core.Models.Commands;

public class CommandResult(IEnumerable<string> errors)
{
    public CommandResult(string error) : this([error]) { }

    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();

    public CommandResult() : this(Array.Empty<string>()) { }
}

public class CommandResult<T>(T data) : CommandResult(Array.Empty<string>())
{
    public T Data { get; } = data;
}
