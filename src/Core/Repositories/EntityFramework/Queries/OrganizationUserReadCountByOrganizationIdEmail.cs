using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class OrganizationUserReadCountByOrganizationIdEmail : IQuery<OrganizationUser>
    {
        private Guid OrganizationId { get; set; }
        private string Email { get; set; }
        private bool OnlyUsers { get; set; }

        public OrganizationUserReadCountByOrganizationIdEmail(Guid organizationId, string email, bool onlyUsers)
        {
            OrganizationId = organizationId;
            Email = email;
            OnlyUsers = onlyUsers;
        }

        public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
        {
            var query = from ou in dbContext.OrganizationUsers
                join u in dbContext.Users
                    on ou.UserId equals u.Id into u_g
                from u in u_g.DefaultIfEmpty()
                where ou.OrganizationId == OrganizationId &&
                    ((!OnlyUsers && (ou.Email == Email || u.Email == Email))
                     || (OnlyUsers && u.Email == Email))
                select ou;
            return query;
        }
    }
}
