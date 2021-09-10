using System.Linq;
using System;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.EntityFramework.Provider;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class ProviderUserReadCountByOnlyOwnerQuery : IQuery<ProviderUser>
    {
        private readonly Guid _userId;

        public ProviderUserReadCountByOnlyOwnerQuery(Guid userId)
        {
            _userId = userId;
        }

        public IQueryable<ProviderUser> Run(DatabaseContext dbContext)
        {
            var owners = from ou in dbContext.ProviderUsers
                where ou.Type == ProviderUserType.ProviderAdmin &&
                ou.Status == ProviderUserStatusType.Confirmed
                group ou by ou.ProviderId into g
                select new 
                { 
                    OrgUser = g.Select(x => new {x.UserId, x.Id}).FirstOrDefault(), ConfirmedOwnerCount = g.Count() 
                };
                    
            var query = from owner in owners
                join ou in dbContext.ProviderUsers
                    on owner.OrgUser.Id equals ou.Id
                where owner.OrgUser.UserId == _userId &&
                    owner.ConfirmedOwnerCount == 1
                select ou;
                                
            return query;
        }
    }
}
