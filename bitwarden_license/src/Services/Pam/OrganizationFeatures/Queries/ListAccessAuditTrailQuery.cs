using Bit.Core.Models.Data;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListAccessAuditTrailQuery : IListAccessAuditTrailQuery
{
    /// <summary>
    /// How many rows one page carries. Fixed rather than caller-supplied: the page size is what bounds the read, so
    /// letting the caller raise it would hand back the unbounded response this replaced.
    /// </summary>
    public const int PageSize = 50;

    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly TimeProvider _timeProvider;

    public ListAccessAuditTrailQuery(
        IAccessAuditEventRepository accessAuditEventRepository,
        TimeProvider timeProvider)
    {
        _accessAuditEventRepository = accessAuditEventRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<AccessAuditEvent>> GetTrailAsync(
        Guid organizationId, AccessAuditTrailQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (since, until) = AccessHistoryWindow.ResolveRange(options.Start, options.End, now);

        // Authorization is the AccessEventLogs permission, enforced at the endpoint, so the trail is org-wide.
        var events = await _accessAuditEventRepository.GetPageByOrganizationIdAsync(organizationId,
            new AccessAuditTrailFilter
            {
                Since = since,
                Until = until,
                PageSize = PageSize,
                Kinds = options.Kinds,
                ActorIds = options.ActorIds,
                IncludeAutomatedActor = options.IncludeAutomatedActor,
                RequesterIds = options.RequesterIds,
                CipherIds = options.CipherIds,
                RuleIds = options.RuleIds,
                BeforeOccurredAt = options.BeforeOccurredAt,
                BeforeId = options.BeforeId,
            });

        var page = new PagedResult<AccessAuditEvent>();
        page.Data.AddRange(events);

        // A full page is the only reason to offer another one. A short page has reached the end of the range, and a
        // full page that happens to be the last costs the caller one more read that comes back empty -- the same
        // bargain the organization event log makes, and the only one available without counting the whole range.
        if (events.Count >= PageSize)
        {
            page.ContinuationToken = AccessAuditTrailContinuationToken.From(page.Data[^1]);
        }

        return page;
    }
}
