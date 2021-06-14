using System.Collections.Generic;
using System.Linq;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Enums;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class UserBumpAccountRevisionDateByOrganizationIdQuery : IQuery<TableModel.User>
    {
        private readonly Guid _organizationId;

        public UserBumpAccountRevisionDateByOrganizationIdQuery(Guid organizationId)
        {
            _organizationId = organizationId;
        }

        public IQueryable<TableModel.User> Run(DatabaseContext dbContext)
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
