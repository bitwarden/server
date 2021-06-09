using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class EventReadPageByCipherIdQuery : IQuery<Event>
    {

        Cipher Cipher { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
        DateTime? BeforeDate { get; set; }
        PageOptions PageOptions { get; set; }

        public EventReadPageByCipherIdQuery(Cipher cipher, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            Cipher = cipher;
            StartDate = startDate;
            EndDate = endDate;
            BeforeDate = null;
            PageOptions = pageOptions;
        }

        public EventReadPageByCipherIdQuery(Cipher cipher, DateTime startDate, DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
        {
            Cipher = cipher;
            StartDate = startDate;
            EndDate = endDate;
            BeforeDate = beforeDate;
            PageOptions = pageOptions;
        }

        public IQueryable<Event> Run(DatabaseContext dbContext)
        {
            var q = from e in dbContext.Events
                    where e.Date >= StartDate &&
                    (BeforeDate == null || e.Date < BeforeDate.Value) &&
                    ((!Cipher.OrganizationId.HasValue && !e.OrganizationId.HasValue) ||
                    (Cipher.OrganizationId.HasValue && Cipher.OrganizationId == e.OrganizationId)) &&
                    ((!Cipher.UserId.HasValue && !e.UserId.HasValue) ||
                     (Cipher.UserId.HasValue && Cipher.UserId == e.UserId)) &&
                    Cipher.Id == e.CipherId
                    orderby e.Date descending
                    select e;
            return q.Skip(0).Take(PageOptions.PageSize);
        }
    }
}
