using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <inheritdoc cref="IListAccessAuditItemsQuery" />
public class ListAccessAuditItemsQuery : IListAccessAuditItemsQuery
{
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly TimeProvider _timeProvider;

    public ListAccessAuditItemsQuery(
        IAccessAuditEventRepository accessAuditEventRepository,
        TimeProvider timeProvider)
    {
        _accessAuditEventRepository = accessAuditEventRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessAuditItem>> GetItemsAsync(
        Guid organizationId, DateTime? start, DateTime? end)
    {
        // The same clamp the page read applies, from the same place, because the two have to agree exactly: a menu
        // built over a wider range than the page it filters would offer options the page can never match.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (since, until) = AccessHistoryWindow.ResolveRange(start, end, now);

        // Authorization is the AccessEventLogs permission, enforced at the endpoint, so this is org-wide -- the same
        // scope as the trail it describes. No page size: the result is one row per distinct subject, which is bounded
        // by how many credentials and rules the organization governs rather than by how much activity there has been.
        return await _accessAuditEventRepository.GetItemsByOrganizationIdAsync(organizationId, since, until);
    }
}
