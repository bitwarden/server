using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// What one page of the PAM access-audit trail is narrowed to. An unset dimension matches everything; the
/// organization is the resource being read, not a filter.
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
    /// Keeps events with no actor, which have no id to select by. Unions with <see cref="ActorIds"/> rather than
    /// narrowing it.
    /// </summary>
    public bool IncludeAutomatedActor { get; init; }

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    /// <summary>
    /// Unions with <c>CipherIds</c> rather than narrowing it, since the two lists are halves of one Item selection.
    /// </summary>
    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>Where the previous page stopped. Null starts at the newest event in range.</summary>
    public AccessAuditEventCursor? Before { get; init; }
}
