using System.Linq;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionReadCountByOrganizationId : IQuery<Collection>
    {
        private Guid OrganizationId { get; set; }

        public CollectionReadCountByOrganizationId(Guid organizationId)
        {
            OrganizationId = organizationId;
        }

        public IQueryable<Collection> Run(DatabaseContext dbContext)
        {
            var query = from c in dbContext.Collections
                where c.OrganizationId == OrganizationId
                select c;
            return query;
        }
    }
}
