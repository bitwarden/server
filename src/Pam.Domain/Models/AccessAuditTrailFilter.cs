using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// What one page of the PAM access-audit trail is narrowed to. Every dimension is independent, and an unset one
/// matches everything. The organization is not here: it is the resource being read, not a filter, and it is what the
/// endpoint authorized.
/// </summary>
public class AccessAuditTrailFilter
{
    /// <summary>Inclusive lower bound on <see cref="AccessAuditEvent.OccurredDate"/>.</summary>
    public required DateTime Since { get; init; }

    /// <summary>Inclusive upper bound on <see cref="AccessAuditEvent.OccurredDate"/>.</summary>
    public required DateTime Until { get; init; }

    public required int PageSize { get; init; }

    public IReadOnlyCollection<AccessAuditEventKind> Kinds { get; init; } = [];

    public IReadOnlyCollection<Guid> ActorIds { get; init; } = [];

    /// <summary>
    /// Whether to keep events with no actor: the automatic ones, which have no id to select by. Unions with
    /// <see cref="ActorIds"/> rather than narrowing it, because an auditor following one approver and the automatic
    /// decisions alongside them is asking for both sets.
    /// </summary>
    public bool IncludeAutomatedActor { get; init; }

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    /// <summary>
    /// Two lists rather than one, because a rule and a cipher are different columns and an id matched against the
    /// wrong one would silently match nothing. They union with each other rather than narrowing, the one place two
    /// dimensions here are OR-ed, because they are the two halves of a single Item selection.
    /// </summary>
    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>Where the previous page stopped. Null starts at the newest event in range.</summary>
    public AccessAuditEventCursor? Before { get; init; }
}
