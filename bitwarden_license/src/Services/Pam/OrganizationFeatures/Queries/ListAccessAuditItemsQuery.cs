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
        // The same clamp the page read applies, from the same place, so the two agree exactly.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (since, until) = AccessHistoryWindow.ResolveRange(start, end, now);

        // Org-wide, the same scope as the trail it describes. No page size: bounded by the organization's
        // credential and rule count rather than by activity volume.
        return await _accessAuditEventRepository.GetItemsByOrganizationIdAsync(organizationId, since, until);
    }
}
