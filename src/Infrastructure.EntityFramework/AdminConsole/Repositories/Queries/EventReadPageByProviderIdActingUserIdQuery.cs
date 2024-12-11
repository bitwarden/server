using Bit.Core.Models.Data;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class EventReadPageByProviderIdActingUserIdQuery : IQuery<Event>
{
    private readonly Guid _providerId;
    private readonly Guid _actingUserId;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly DateTime? _beforeDate;
    private readonly PageOptions _pageOptions;

    public EventReadPageByProviderIdActingUserIdQuery(
        Guid providerId,
        Guid actingUserId,
        DateTime startDate,
        DateTime endDate,
        DateTime? beforeDate,
        PageOptions pageOptions
    )
    {
        _providerId = providerId;
        _actingUserId = actingUserId;
        _startDate = startDate;
        _endDate = endDate;
        _beforeDate = beforeDate;
        _pageOptions = pageOptions;
    }

    public IQueryable<Event> Run(DatabaseContext dbContext)
    {
        var q =
            from e in dbContext.Events
            where
                e.Date >= _startDate
                && (_beforeDate != null || e.Date <= _endDate)
                && (_beforeDate == null || e.Date < _beforeDate.Value)
                && e.ProviderId == _providerId
                && e.ActingUserId == _actingUserId
            orderby e.Date descending
            select e;
        return q.Skip(0).Take(_pageOptions.PageSize);
    }
}
