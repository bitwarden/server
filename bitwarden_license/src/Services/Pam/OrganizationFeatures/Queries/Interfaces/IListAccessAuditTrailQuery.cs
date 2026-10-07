using Bit.Core.Models.Data;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

/// <summary>
/// What one read of the access-audit trail asks for. An unset dimension matches everything; the bounds are as
/// requested, before the retention window is applied.
/// </summary>
public class AccessAuditTrailQueryOptions
{
    /// <summary>Inclusive lower bound. Null reaches back as far as the retention window allows.</summary>
    public DateTime? Start { get; init; }

    /// <summary>Inclusive upper bound. Null reaches up to now.</summary>
    public DateTime? End { get; init; }

    public IReadOnlyCollection<AccessAuditEventKind> Kinds { get; init; } = [];

    public IReadOnlyCollection<Guid> ActorIds { get; init; } = [];

    /// <summary>Whether to include system events, which have no actor.</summary>
    public bool IncludeAutomatedActor { get; init; }

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];

    /// <summary>The subject ciphers to keep. Unions with <see cref="RuleIds"/>.</summary>
    public IReadOnlyCollection<Guid> CipherIds { get; init; } = [];

    public IReadOnlyCollection<Guid> RuleIds { get; init; } = [];

    /// <summary>Where the previous page stopped. Null starts at the newest event.</summary>
    public AccessAuditEventCursor? Before { get; init; }
}

/// <summary>Reads the distinct subjects the trail names in a range, for the Item filter.</summary>
public interface IListAccessAuditItemsQuery
{
    /// <summary>
    /// The ciphers and access rules the organization's trail names between <paramref name="start"/> and
    /// <paramref name="end"/>, clamped to the history window as the trail read is.
    /// </summary>
    Task<ICollection<AccessAuditItem>> GetItemsAsync(Guid organizationId, DateTime? start, DateTime? end);
}

public interface IListAccessAuditTrailQuery
{
    /// <summary>
    /// Returns one page of the organization's access-audit trail, newest first, matching <paramref name="options"/>
    /// and clamped to the history window. The continuation token is set while more pages remain.
    /// </summary>
    Task<PagedResult<AccessAuditEvent>> GetTrailAsync(Guid organizationId, AccessAuditTrailQueryOptions options);
}
