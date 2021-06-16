using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;
using Bit.Core.Models.Data;
using Table = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class EventReadPageByCipherIdQuery : IQuery<Event>
    {
        private readonly Table.Cipher _cipher;
        private readonly DateTime _startDate;
        private readonly DateTime _endDate;
        private readonly DateTime? _beforeDate;
        private readonly PageOptions _pageOptions;

        public EventReadPageByCipherIdQuery(Table.Cipher cipher, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            _cipher = cipher;
            _startDate = startDate;
            _endDate = endDate;
            _beforeDate = null;
            _pageOptions = pageOptions;
        }

        public EventReadPageByCipherIdQuery(Table.Cipher cipher, DateTime startDate, DateTime endDate, DateTime? beforeDate, PageOptions pageOptions)
        {
            _cipher = cipher;
            _startDate = startDate;
            _endDate = endDate;
            _beforeDate = beforeDate;
            _pageOptions = pageOptions;
        }

        public IQueryable<Event> Run(DatabaseContext dbContext)
        {
            var q = from e in dbContext.Events
                where e.Date >= _startDate &&
                (_beforeDate == null || e.Date < _beforeDate.Value) &&
                ((!_cipher.OrganizationId.HasValue && !e.OrganizationId.HasValue) ||
                (_cipher.OrganizationId.HasValue && _cipher.OrganizationId == e.OrganizationId)) &&
                ((!_cipher.UserId.HasValue && !e.UserId.HasValue) ||
                    (_cipher.UserId.HasValue && _cipher.UserId == e.UserId)) &&
                _cipher.Id == e.CipherId
                orderby e.Date descending
                select e;
            return q.Skip(0).Take(_pageOptions.PageSize);
        }
    }
}
