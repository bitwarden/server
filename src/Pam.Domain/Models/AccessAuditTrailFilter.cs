using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// What one page of the PAM access-audit trail is narrowed to: a time range, selectable dimensions, and where the
/// previous page stopped. Every dimension is independent, and an unset one matches everything. The bounds arrive
/// already clamped to the shared retention window (<c>AccessHistoryWindow</c>); the organization is not a filter
/// here since it is what the endpoint authorized.
/// </summary>
public class AccessAuditTrailFilter
{
    /// <summary>Inclusive lower bound on <see cref="AccessAuditEvent.OccurredAt"/>.</summary>
    public required DateTime Since { get; init; }

    /// <summary>Inclusive upper bound on <see cref="AccessAuditEvent.OccurredAt"/>.</summary>
    public required DateTime Until { get; init; }

    /// <summary>How many rows the page may carry. The read returns at most this many.</summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// The kinds to keep. Empty means every kind.
    /// </summary>
    public IReadOnlyCollection<AccessAuditEventKind> Kinds { get; init; } = [];

    /// <summary>
    /// The actors to keep, by user id. Empty — with <see cref="IncludeAutomatedActor"/> unset — means every actor.
    /// </summary>
    public IReadOnlyCollection<Guid> ActorIds { get; init; } = [];

    /// <summary>
    /// Whether to keep events with no actor, shown in the trail as "System". Unions with <see cref="ActorIds"/>
    /// rather than narrowing it.
    /// </summary>
    public bool IncludeAutomatedActor { get; init; }

    /// <summary>The requesters to keep, by user id. Empty means every requester.</summary>
    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    /// <summary>
    /// The subject ciphers to keep. Empty — with <see cref="RuleIds"/> also empty — means every subject.
    /// </summary>
    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    /// <summary>
    /// The subject access rules to keep. Separate from <see cref="CipherIds"/> since a rule-administration event
    /// has no cipher; the two UNION rather than narrow.
    /// </summary>
    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>
    /// Where the previous page stopped: its last row's <see cref="AccessAuditEvent.OccurredAt"/>. Null starts at the
    /// newest event in range. Paired with <see cref="BeforeId"/>, which resolves a boundary landing inside a group of
    /// events sharing one instant — the before/after halves of an action are written with the same
    /// <see cref="AccessAuditEvent.OccurredAt"/>, so those groups are ordinary rather than exceptional here.
    /// </summary>
    public DateTime? BeforeOccurredAt { get; init; }

    /// <summary>The previous page's last row id, paired with <see cref="BeforeOccurredAt"/>.</summary>
    public Guid? BeforeId { get; init; }
}
