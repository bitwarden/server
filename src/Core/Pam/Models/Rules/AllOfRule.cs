namespace Bit.Core.Pam.Models.Rules;

/// <summary>
/// Composite rule that approves only when every child rule approves.
/// </summary>
public sealed class AllOfRule : Rule
{
    public IReadOnlyList<Rule> Rules { get; init; } = [];
}
