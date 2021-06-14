using System.Linq;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Enums;
using System.Collections.Generic;
using Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class UserCipherDetailsQuery : IQuery<CipherDetails>
    {
        private readonly Guid? _userId; 
        public UserCipherDetailsQuery(Guid? userId)
        {
            _userId = userId;
        }
        public virtual IQueryable<CipherDetails> Run(DatabaseContext dbContext)
        {
            var query = from cd in new CipherDetailsQuery(_userId, true).Run(dbContext)
                        join ou in dbContext.OrganizationUsers
                            on cd.OrganizationId equals ou.OrganizationId
                        where ou.UserId == _userId &&
                            ou.Status == OrganizationUserStatusType.Confirmed
                        join o in dbContext.Organizations 
                            on cd.OrganizationId equals o.Id
                        where o.Id == ou.OrganizationId && o.Enabled
                        join cc in dbContext.CollectionCiphers
                            on cd.Id equals cc.CipherId into cc_g
                        from cc in cc_g.DefaultIfEmpty()
                        where ou.AccessAll
                        join cu in dbContext.CollectionUsers
                            on cc.CollectionId equals cu.CollectionId into cu_g
                        from cu in cu_g.DefaultIfEmpty()
                        where cu.OrganizationUserId == ou.Id
                        join gu in dbContext.GroupUsers
                            on ou.Id equals gu.OrganizationUserId into gu_g
                        from gu in gu_g.DefaultIfEmpty()
                        where cu.CollectionId == null && !ou.AccessAll
                        join g in dbContext.Groups
                            on gu.GroupId equals g.Id into g_g
                        from g in g_g.DefaultIfEmpty()
                        join cg in dbContext.CollectionGroups
                            on cc.CollectionId equals cg.CollectionId into cg_g
                        from cg in cg_g.DefaultIfEmpty()
                        where !g.AccessAll && cg.GroupId == gu.GroupId &&
                        ou.AccessAll || cu.CollectionId != null || g.AccessAll || cg.CollectionId != null
                        select new {cd, ou, o, cc, cu, gu, g, cg}.cd;

            var query2 = from cd in new CipherDetailsQuery(_userId, true).Run(dbContext)
                     where cd.UserId == _userId
                     select cd;

            return query.Union(query2);
        }
    }
}
