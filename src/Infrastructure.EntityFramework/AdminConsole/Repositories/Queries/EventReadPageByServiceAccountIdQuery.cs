using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Event = Bit.Infrastructure.EntityFramework.Models.Event;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class EventReadPageByServiceAccountQuery : IQuery<Event>
{
    private readonly ServiceAccount _serviceAccount;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly DateTime? _beforeDate;
    private readonly PageOptions _pageOptions;

    public EventReadPageByServiceAccountQuery(ServiceAccount serviceAccount, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        _serviceAccount = serviceAccount;
        _startDate = startDate;
        _endDate = endDate;
        _beforeDate = null;
        _pageOptions = pageOptions;
    }

    public EventReadPageByServiceAccountQuery(ServiceAccount serviceAccount, DateTime startDate, DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
    {
        _serviceAccount = serviceAccount;
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
                        (_serviceAccount.OrganizationId == emptyGuid && !e.OrganizationId.HasValue) ||
                        (_serviceAccount.OrganizationId != emptyGuid && e.OrganizationId == _serviceAccount.OrganizationId)
                    ) &&
                    e.GrantedServiceAccountId == _serviceAccount.Id
                orderby e.Date descending
                select e;

        return q.Take(_pageOptions.PageSize);
    }
}
