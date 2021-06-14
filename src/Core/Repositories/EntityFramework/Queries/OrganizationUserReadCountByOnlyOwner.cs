using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserReadCountByOnlyOwner : IQuery<OrganizationUser>
    {
        private readonly Guid _userId;

        public OrganizationUserReadCountByOnlyOwner(Guid userId)
        {
            _userId = userId;
        }

        public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
        {
            var owners = from ou in dbContext.OrganizationUsers
                             where ou.Type == OrganizationUserType.Owner &&
                                ou.Status == OrganizationUserStatusType.Confirmed
                             group ou by ou.OrganizationId into g
                             select new { 
                                 OrgUser = g.Select(x => new {x.UserId, x.Id}).FirstOrDefault(), ConfirmedOwnerCount = g.Count() 
                             };
                    
            var query = from owner in owners
                        join ou in dbContext.OrganizationUsers
                            on owner.OrgUser.Id equals ou.Id
                        where owner.OrgUser.UserId == _userId &&
                            owner.ConfirmedOwnerCount == 1
                        select new { owner, ou };
                                
            return query.Select(x => x.ou);
        }
    }
}
