using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class EventReadPageByOrganizationIdQuery: IQuery<Event>
    {
        private readonly Guid _organizationId;
        private readonly DateTime _startDate;
        private readonly DateTime _endDate;
        private readonly DateTime? _beforeDate;
        private readonly PageOptions _pageOptions;

        public EventReadPageByOrganizationIdQuery(Guid organizationId, DateTime startDate,
                DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
        {
            _organizationId = organizationId;
            _startDate = startDate;
            _endDate = endDate;
            _beforeDate = beforeDate;
            _pageOptions = pageOptions;
        }

        public IQueryable<Event> Run(DatabaseContext dbContext)
        {
            var q = from e in dbContext.Events
                where e.Date >= _startDate &&
                (_beforeDate != null || e.Date <= _endDate) &&
                (_beforeDate == null || e.Date < _beforeDate.Value) &&
                e.OrganizationId == _organizationId
                orderby e.Date descending
                select e;
            return q.Skip(0).Take(_pageOptions.PageSize);
        }
    }
}
