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

    /// <summary>
    /// The rule's pre-fill duration for a request opened under it, in seconds. Null means the rule stores no default
    /// and the global one applies. Resolve through <see cref="LeaseDurationBounds"/> rather than reading it raw — a
    /// rule may store a default that exceeds its own <see cref="MaxLeaseDurationSeconds"/>.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; init; }

    /// <summary>
    /// The rule's own ceiling on a single lease, in seconds. Null means no per-rule cap, leaving only the global one.
    /// Resolve through <see cref="LeaseDurationBounds"/> rather than reading it raw, so the global ceiling is applied
    /// alongside it.
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; init; }

    /// <summary>
    /// The rule's conditions minus its human-approval gate: the ones a machine can decide on its own, at any point in
    /// a request's life. The gate is excluded because it is a submit-time routing decision that an approver's verdict
    /// settles once and for all -- folding it back in would make every later re-evaluation return requires-approval
    /// forever, and there is no second approver to route to. These are what activation re-checks before minting.
    /// </summary>
    public IReadOnlyList<AccessCondition> AutomatedConditions =>
        Conditions.Where(condition => condition is not HumanApprovalCondition).ToList();

    /// <summary>
    /// True when the stored conditions document could not be parsed, so <see cref="Conditions"/> holds the resolver's
    /// fail-safe stand-in rather than what the admin configured. A caller that routes to a human is already safe --
    /// the stand-in <em>is</em> a human-approval gate -- but a caller that evaluates <see cref="AutomatedConditions"/>
    /// must refuse outright: the stand-in leaves that list empty, and an empty list is vacuously satisfied, so
    /// deferring to it would fail open on exactly the documents the server could not understand.
    /// </summary>
    public bool ConditionsUnreadable { get; init; }
}
