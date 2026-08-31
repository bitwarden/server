using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// What one page of the PAM access-audit trail is narrowed to: a time range, the dimensions an auditor can select on,
/// and where the previous page stopped. Every dimension is independent, and an unset one matches everything.
///
/// The bounds arrive already clamped to the store's retention window — it holds no promise about anything older, so
/// the read never asks for it. The organization is not on here: it is the resource being read, not a filter, and it is
/// what the endpoint authorized.
/// </summary>
public class AccessAuditTrailFilter
{
    /// <summary>Inclusive lower bound on <see cref="AccessAuditEvent.OccurredDate"/>.</summary>
    public required DateTime Since { get; init; }

    /// <summary>Inclusive upper bound on <see cref="AccessAuditEvent.OccurredDate"/>.</summary>
    public required DateTime Until { get; init; }

    /// <summary>How many rows the page may carry. The read returns at most this many.</summary>
    public required int PageSize { get; init; }

    /// <summary>The kinds to keep. Empty means every kind.</summary>
    public IReadOnlyCollection<AccessAuditEventKind> Kinds { get; init; } = [];

    /// <summary>
    /// The actors to keep, by user id. Empty — with <see cref="IncludeAutomatedActor"/> unset — means every actor.
    /// </summary>
    public IReadOnlyCollection<Guid> ActorIds { get; init; } = [];

    /// <summary>
    /// Whether to keep events with no actor — the system / automatic ones, which the trail renders as "System" and
    /// which therefore have no id to select by. Unions with <see cref="ActorIds"/> rather than narrowing it: an auditor
    /// following one approver and the automatic decisions alongside them is asking for both sets.
    /// </summary>
    public bool IncludeAutomatedActor { get; init; }

    /// <summary>The requesters to keep, by user id. Empty means every requester.</summary>
    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    /// <summary>
    /// The subject ciphers to keep. Empty — with <see cref="RuleIds"/> also empty — means every subject.
    /// </summary>
    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    /// <summary>
    /// The subject access rules to keep.
    ///
    /// Two lists rather than one, because a rule-administration event names a rule and no cipher: they are different
    /// columns, and an id matched against the wrong one would silently match nothing. But they UNION with each other
    /// rather than narrowing, which is the one place two dimensions here are OR-ed: they are the two halves of a single
    /// Item selection, and an auditor picking one credential and one rule is asking for both, not for the empty
    /// intersection of the two.
    /// </summary>
    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>
    /// Where the previous page stopped — its last row. Null starts at the newest event in range. Both halves of the
    /// cursor are needed because <see cref="AccessAuditEvent.OccurredDate"/> is not unique: an action writes its
    /// before/after halves at one instant, so a boundary landing inside a group of events sharing a timestamp is
    /// ordinary here rather than exceptional.
    /// </summary>
    public AccessAuditEventCursor? Before { get; init; }
}
