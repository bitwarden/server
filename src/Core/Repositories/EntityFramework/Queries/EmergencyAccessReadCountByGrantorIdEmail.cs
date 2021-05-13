using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class EmergencyAccessReadCountByGrantorIdEmail : IQuery<EmergencyAccess>
    {
        private Guid GrantorId { get; set; }
        private string Email { get; set; }
        private bool OnlyRegisteredUsers { get; set; }

        public EmergencyAccessReadCountByGrantorIdEmail(Guid grantorId, string email, bool onlyRegisteredUsers)
        {
            GrantorId = grantorId;
            Email = email;
            OnlyRegisteredUsers = onlyRegisteredUsers;
        }

        public IQueryable<EmergencyAccess> Run(DatabaseContext dbContext)
        {
            var query = from ea in dbContext.EmergencyAccesses
                join u in dbContext.Users
                    on ea.GranteeId equals u.Id into u_g
                from u in u_g.DefaultIfEmpty()
                where ea.GrantorId == GrantorId &&
                    ((!OnlyRegisteredUsers && (ea.Email == Email || u.Email == Email))
                     || (OnlyRegisteredUsers && u.Email == Email))
                select new { ea, u };
            return query.Select(x => x.ea);
        }
    }
}
