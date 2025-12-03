using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Event = Bit.Infrastructure.EntityFramework.Models.Event;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class EventReadPageByProjectQuery : IQuery<Event>
{
    private readonly Project _project;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly DateTime? _beforeDate;
    private readonly PageOptions _pageOptions;

    public EventReadPageByProjectQuery(Project project, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        _project = project;
        _startDate = startDate;
        _endDate = endDate;
        _beforeDate = null;
        _pageOptions = pageOptions;
    }

    public EventReadPageByProjectQuery(Project project, DateTime startDate, DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
    {
        _project = project;
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
                        (_project.OrganizationId == emptyGuid && !e.OrganizationId.HasValue) ||
                        (_project.OrganizationId != emptyGuid && e.OrganizationId == _project.OrganizationId)
                    ) &&
                    e.ProjectId == _project.Id
                orderby e.Date descending
                select e;

        return q.Take(_pageOptions.PageSize);
    }
}
