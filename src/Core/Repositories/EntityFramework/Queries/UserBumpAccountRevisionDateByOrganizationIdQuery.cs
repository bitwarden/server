using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using System;
using Bit.Core.Models.EntityFramework;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class UserBumpAccountRevisionDateByOrganizationIdQuery : IQuery<User>
    {
        private readonly Guid _organizationId;

        public UserBumpAccountRevisionDateByOrganizationIdQuery(Guid organizationId)
        {
            _organizationId = organizationId;
        }

        public IQueryable<User> Run(DatabaseContext dbContext)
        {
            var query = from u in dbContext.Users
                join ou in dbContext.OrganizationUsers
                    on u.Id equals ou.UserId
                where ou.OrganizationId == _organizationId &&
                    ou.Status == OrganizationUserStatusType.Confirmed
                select new { u, ou };
                        
            return query.Select(x => x.u);
        }
    }
}
