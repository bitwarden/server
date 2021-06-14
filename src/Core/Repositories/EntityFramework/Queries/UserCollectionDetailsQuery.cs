using System.Linq;
using Bit.Core.Models.Table;
using System;
using Bit.Core.Enums;
using System.Collections.Generic;
using Core.Models.Data;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class UserCollectionDetailsQuery : IQuery<CollectionDetails>
    {
        private readonly Guid? _userId; 
        public UserCollectionDetailsQuery(Guid? userId)
        {
            _userId = userId;
        }
        public virtual IQueryable<CollectionDetails> Run(DatabaseContext dbContext)
        {
            var query = from c in dbContext.Collections
                        join ou in dbContext.OrganizationUsers
                            on c.OrganizationId equals ou.OrganizationId
                        join o in dbContext.Organizations
                            on c.OrganizationId equals o.Id
                        join cu in dbContext.CollectionUsers
                            on c.Id equals cu.CollectionId into cu_g
                        from cu in cu_g.DefaultIfEmpty()
                        where ou.AccessAll && cu.OrganizationUserId == ou.Id
                        join gu in dbContext.GroupUsers
                            on ou.Id equals gu.OrganizationUserId into gu_g
                        from gu in gu_g.DefaultIfEmpty()
                        where cu.CollectionId == null && !ou.AccessAll
                        join g in dbContext.Groups
                            on gu.GroupId equals g.Id into g_g
                        from g in g_g.DefaultIfEmpty()
                        join cg in dbContext.CollectionGroups
                            on gu.GroupId equals cg.GroupId into cg_g
                        from cg in cg_g.DefaultIfEmpty()
                        where !g.AccessAll && cg.CollectionId == c.Id &&
                            ou.UserId == _userId &&
                            ou.Status == OrganizationUserStatusType.Confirmed &&
                            o.Enabled &&
                            (ou.AccessAll || cu.CollectionId != null || g.AccessAll || cg.CollectionId != null)
                        select new { c, ou, o, cu, gu, g, cg };
            return query.Select(x => new CollectionDetails() {
                Id = x.c.Id,
                OrganizationId = x.c.OrganizationId,
                Name = x.c.Name,
                ExternalId = x.c.ExternalId,
                CreationDate = x.c.CreationDate,
                RevisionDate = x.c.RevisionDate,
                ReadOnly = !x.ou.AccessAll || !x.g.AccessAll || (x.cu.ReadOnly || x.cg.ReadOnly),
                HidePasswords = !x.ou.AccessAll || !x.g.AccessAll || (x.cu.HidePasswords || x.cg.HidePasswords),
            });
        }
    }
}
