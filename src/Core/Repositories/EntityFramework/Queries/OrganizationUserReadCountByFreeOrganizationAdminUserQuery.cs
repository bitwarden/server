using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.EntityFramework;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserReadCountByFreeOrganizationAdminUserQuery : IQuery<OrganizationUser>
    {
        private readonly Guid _userId;

        public OrganizationUserReadCountByFreeOrganizationAdminUserQuery(Guid userId)
        {
            _userId = userId;
        }

        public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                join o in dbContext.Organizations
                    on ou.OrganizationId equals o.Id
                where ou.UserId == _userId &&
                    (ou.Type == OrganizationUserType.Owner || ou.Type == OrganizationUserType.Admin) &&
                    o.PlanType == PlanType.Free &&
                    ou.Status == OrganizationUserStatusType.Confirmed
                select new { ou, o };
                                
            return query.Select(x => x.ou);
        }
    }
}
