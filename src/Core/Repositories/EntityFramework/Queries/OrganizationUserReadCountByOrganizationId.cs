using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserReadCountByOrganizationId : IQuery<OrganizationUser>
    {
        private Guid OrganizationId { get; set; }

        public OrganizationUserReadCountByOrganizationId(Guid organizationId)
        {
            OrganizationId = organizationId;
        }

        public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                where ou.OrganizationId == OrganizationId
                select ou;
            return query;
        }
    }
}
