using Bit.Core.Models.Data;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// What one read of the access-audit trail asks for, as the endpoint's validated query parameters. Every dimension is
/// optional and an unset one matches everything; the bounds are what the caller asked for, before the retention
/// window has been applied to them.
/// </summary>
public class AccessAuditTrailQueryOptions
{
    /// <summary>Inclusive lower bound. Null reaches back as far as the retention window allows.</summary>
    public DateTime? Start { get; init; }

    /// <summary>Inclusive upper bound. Null reaches up to now.</summary>
    public DateTime? End { get; init; }

    public IReadOnlyCollection<AccessAuditEventKind> Kinds { get; init; } = [];

    public IReadOnlyCollection<Guid> ActorIds { get; init; } = [];

    /// <summary>Whether to include the system / automatic events, which have no actor id to select by.</summary>
    public bool IncludeAutomatedActor { get; init; }

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    /// <summary>
    /// The subject ciphers and access rules to keep. They union with each other rather than narrowing: the two are the
    /// halves of one Item selection, since a rule-administration event names a rule and no cipher.
    /// </summary>
    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>Where the previous page stopped, already read back off the wire. Null starts at the newest event.</summary>
    public DateTime? BeforeOccurredAt { get; init; }

    /// <summary>The previous page's last row id. Set whenever <see cref="BeforeOccurredAt"/> is.</summary>
    public Guid? BeforeId { get; init; }
}

/// <summary>
/// Reads the distinct subjects the trail names in a range, which is what the Item filter's menu is built from.
/// Separate from <see cref="IListAccessAuditTrailQuery"/> because it answers a different question about the same
/// resource: not "what happened" but "what could you ask about".
/// </summary>
public interface IListAccessAuditItemsQuery
{
    /// <summary>
    /// The ciphers and access rules the organization's trail names between <paramref name="start"/> and
    /// <paramref name="end"/>. The bounds are clamped to the shared history window exactly as the page read clamps
    /// them, so the menu cannot offer an option the page it filters can never match.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsAsync(Guid organizationId, DateTime? start, DateTime? end);
}

public interface IListAccessAuditTrailQuery
{
    /// <summary>
    /// Returns one page of the org-wide access-audit trail for <paramref name="organizationId"/>, newest first — the
    /// access-request, access-lease, rule-administration and rotation events in the organization that match
    /// <paramref name="options"/>. Authorization (the AccessEventLogs permission) is enforced at the endpoint before
    /// this runs. Events are read from the dedicated append-only audit store, where each was written (self-contained)
    /// at the moment it happened, and each action's before/after pair is collapsed there rather than here — the
    /// collapse has to happen before the page is cut to survive a page boundary.
    ///
    /// The requested bounds are clamped to the shared history window, so no parameter reaches further back than the
    /// store promises to hold. The result's continuation token is set only while more pages remain.
    /// </summary>
    Task<PagedResult<AccessAuditEvent>> GetTrailAsync(Guid organizationId, AccessAuditTrailQueryOptions options);
}
