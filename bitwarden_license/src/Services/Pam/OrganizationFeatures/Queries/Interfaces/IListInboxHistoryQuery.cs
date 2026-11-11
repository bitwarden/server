using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxHistoryQuery
{
    /// <summary>
    /// Returns one page of the requests that have ended within the history window, on the collections the user can
    /// Manage and among the user's own, newest end first.
    /// </summary>
    Task<AccessRequestHistoryPage> GetHistoryAsync(Guid userId, DateTime now, AccessRequestHistoryQueryOptions options);
}

/// <summary>Which page of the history to read, and what to narrow it to. An empty list matches everything.</summary>
public class AccessRequestHistoryQueryOptions
{
    /// <summary>0-based.</summary>
    public int Page { get; init; }

    public required int PageSize { get; init; }

    public IReadOnlyCollection<AccessRequestHistoryStatus> Statuses { get; init; } = [];

    public IReadOnlyCollection<Guid> CollectionIds { get; init; } = [];

    public IReadOnlyCollection<Guid> RequesterIds { get; init; } = [];
}
