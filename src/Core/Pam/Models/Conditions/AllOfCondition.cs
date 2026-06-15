namespace Bit.Core.Pam.Models.Conditions;

/// <summary>
/// Composite condition that approves only when every child condition approves. An empty set of children is
/// vacuously satisfied (always allow), representing a rule with no gating conditions that exists only to route
/// access through the PAM flow for audit logging.
/// </summary>
public sealed class AllOfCondition : AccessCondition
{
    public IReadOnlyList<AccessCondition> Conditions { get; init; } = [];
}
