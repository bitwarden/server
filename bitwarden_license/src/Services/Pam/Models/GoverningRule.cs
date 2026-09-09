using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Models;

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
    /// The identity of the resolved access rule. Resolution is deterministic (oldest rule wins; see
    /// <see cref="Services.IGoverningRuleResolver"/>), so this is the rule that a request should pin at submit once
    /// pinning is persisted. Until then it is re-resolved on every operation and can drift if the governing rules
    /// change between submit and a later read.
    /// </summary>
    public Guid RuleId { get; init; }

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
