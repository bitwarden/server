namespace Bit.Core.Pam.Models.Conditions;

/// <summary>
/// Composite condition that approves only when every child condition approves.
/// </summary>
public sealed class AllOfCondition : AccessCondition
{
    public IReadOnlyList<AccessCondition> Conditions { get; init; } = [];
}
