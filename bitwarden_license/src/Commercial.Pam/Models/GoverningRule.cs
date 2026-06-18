using Bit.Commercial.Pam.Models.Conditions;

namespace Bit.Commercial.Pam.Models;

/// <summary>
/// The access rule that governs a cipher for a particular caller: which collection's rule applies, the owning
/// organization, whether the rule requires human approval, and the parsed flat list of <see cref="AccessCondition"/>s
/// so the rule engine can evaluate them against the caller's signals. A null governing rule means the cipher is not
/// leasing-gated for the caller.
/// </summary>
public sealed record GoverningRule(
    Guid OrganizationId,
    Guid CollectionId,
    bool RequiresHumanApproval,
    IReadOnlyList<AccessCondition> Conditions)
{
    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it once (always auto-approved), by up
    /// to <see cref="MaxExtensionDurationSeconds"/>.
    /// </summary>
    public bool AllowsExtensions { get; init; }

    /// <summary>
    /// The longest a single extension under this rule may run, in seconds; meaningful only when
    /// <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; init; }
}
