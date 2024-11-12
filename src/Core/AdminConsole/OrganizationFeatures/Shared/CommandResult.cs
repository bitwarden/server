namespace Bit.Core.AdminConsole.OrganizationFeatures.Shared;

public class CommandResult(IEnumerable<string> errors)
{
    public bool Success => ErrorMessages.Count == 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public List<string> ErrorMessages { get; } = errors.ToList();

    public CommandResult() : this([]) { }
}
