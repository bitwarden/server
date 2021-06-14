using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserReadCountByOrganizationIdQuery : IQuery<OrganizationUser>
    {
        private readonly Guid _organizationId;

        public OrganizationUserReadCountByOrganizationIdQuery(Guid organizationId)
        {
            _organizationId = organizationId;
        }

        public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                where ou.OrganizationId == _organizationId
                select ou;
            return query;
        }
    }
}
