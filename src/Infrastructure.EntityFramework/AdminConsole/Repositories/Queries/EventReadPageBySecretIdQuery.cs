using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Event = Bit.Infrastructure.EntityFramework.Models.Event;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class EventReadPageBySecretQuery : IQuery<Event>
{
    private readonly Secret _secret;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly DateTime? _beforeDate;
    private readonly PageOptions _pageOptions;

    public EventReadPageBySecretQuery(Secret secret, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        _secret = secret;
        _startDate = startDate;
        _endDate = endDate;
        _beforeDate = null;
        _pageOptions = pageOptions;
    }

    public EventReadPageBySecretQuery(Secret secret, DateTime startDate, DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
    {
        _secret = secret;
        _startDate = startDate;
        _endDate = endDate;
        _beforeDate = beforeDate;
        _pageOptions = pageOptions;
    }

    public IQueryable<Event> Run(DatabaseContext dbContext)
    {
        var emptyGuid = Guid.Empty;
        var q = from e in dbContext.Events
                where e.Date >= _startDate &&
                    (_beforeDate == null || e.Date < _beforeDate.Value) &&
                    (
                        (_secret.OrganizationId == emptyGuid && !e.OrganizationId.HasValue) ||
                        (_secret.OrganizationId != emptyGuid && e.OrganizationId == _secret.OrganizationId)
                    ) &&
                    e.SecretId == _secret.Id
                orderby e.Date descending
                select e;

        return q.Take(_pageOptions.PageSize);
    }
}
