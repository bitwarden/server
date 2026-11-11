using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One page of the approver history: the ended requests on the collections the caller can Manage, and the caller's
/// own wherever they are. An empty filter list matches everything.
/// </summary>
public class AccessRequestHistoryFilter
{
    public required IReadOnlyCollection<Guid> ManageableCollectionIds { get; init; }

    /// <summary>The caller, whose own requests are read too.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Inclusive lower bound on the request's creation.</summary>
    public required DateTime Since { get; init; }

    /// <summary>The clock that decides which requests have ended.</summary>
    public required DateTime Now { get; init; }

    public required int Skip { get; init; }

    public required int Take { get; init; }

    public IReadOnlyCollection<AccessRequestHistoryStatus> Statuses { get; init; } = [];

    public IReadOnlyCollection<Guid> CollectionIds { get; init; } = [];

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];
}
