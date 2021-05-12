using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class PolicyReadByUserId : IQuery<Policy>
    {
        private Guid UserId { get; set; }

        public PolicyReadByUserId(Guid userId)
        {
            UserId = userId;
        }

        public IQueryable<Policy> Run(DatabaseContext dbContext)
        {
            var query = from p in dbContext.Policies
                        join ou in dbContext.OrganizationUsers
                            on p.OrganizationId equals ou.OrganizationId
                        join o in dbContext.Organizations
                            on ou.OrganizationId equals o.Id
                        where ou.UserId == UserId &&
                            ou.Status == OrganizationUserStatusType.Confirmed &&
                            o.Enabled == true
                        select new { p, ou, o };
                                
            return query.Select(x => x.p);
        }
    }
}
