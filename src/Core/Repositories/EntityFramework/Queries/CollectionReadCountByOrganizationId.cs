using System.Linq;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionReadCountByOrganizationId : IQuery<Collection>
    {
        private readonly Guid _organizationId;

        public CollectionReadCountByOrganizationId(Guid organizationId)
        {
            _organizationId = organizationId;
        }

        public IQueryable<Collection> Run(DatabaseContext dbContext)
        {
            var query = from c in dbContext.Collections
                where c.OrganizationId == _organizationId
                select c;
            return query;
        }
    }
}
