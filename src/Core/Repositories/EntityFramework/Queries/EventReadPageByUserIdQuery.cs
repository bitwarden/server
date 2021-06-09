using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class EventReadPageByUserIdQuery: IQuery<Event>
    {

        Guid UserId { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
        DateTime? BeforeDate { get; set; }
        PageOptions PageOptions { get; set; }

        public EventReadPageByUserIdQuery(Guid userId, DateTime startDate,
                DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
        {
            UserId = userId;
            StartDate = startDate;
            EndDate = endDate;
            BeforeDate = beforeDate;
            PageOptions = pageOptions;
        }

        public IQueryable<Event> Run(DatabaseContext dbContext)
        {
            var q = from e in dbContext.Events
                    where e.Date >= StartDate &&
                    (BeforeDate != null || e.Date <= EndDate) &&
                    (BeforeDate == null || e.Date < BeforeDate.Value) &&
                    !e.OrganizationId.HasValue &&
                    e.ActingUserId == UserId
                    orderby e.Date descending
                    select e;
            return q.Skip(0).Take(PageOptions.PageSize);
        }
    }
}
