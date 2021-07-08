using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;
using Bit.Core.Enums;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CipherReadCanEditByIdUserIdQuery : IQuery<Cipher>
    {
        private readonly Guid _userId;
        private readonly Guid _cipherId;

        public CipherReadCanEditByIdUserIdQuery(Guid userId, Guid cipherId)
        {
            _userId = userId;
            _cipherId = cipherId;
        }

        public virtual IQueryable<Cipher> Run(DatabaseContext dbContext)
        {
            var query = from c in dbContext.Ciphers
                join o in dbContext.Organizations
                    on c.OrganizationId equals o.Id into o_g
                from o in o_g.DefaultIfEmpty()
                where !c.UserId.HasValue
                join ou in dbContext.OrganizationUsers
                    on o.Id equals ou.OrganizationId into ou_g
                from ou in ou_g.DefaultIfEmpty()
                where ou.UserId == _userId
                join cc in dbContext.CollectionCiphers
                    on c.Id equals cc.CipherId into cc_g
                from cc in cc_g.DefaultIfEmpty()
                where !c.UserId.HasValue && !ou.AccessAll
                join cu in dbContext.CollectionUsers
                    on cc.CollectionId equals cu.CollectionId into cu_g
                from cu in cu_g.DefaultIfEmpty()
                where ou.Id == cu.OrganizationUserId
                join gu in dbContext.GroupUsers
                    on ou.Id equals gu.OrganizationUserId into gu_g
                from gu in gu_g.DefaultIfEmpty()
                where !c.UserId.HasValue  && cu.CollectionId == null && !ou.AccessAll
                join g in dbContext.Groups
                    on gu.GroupId equals g.Id into g_g
                from g in g_g.DefaultIfEmpty()
                join cg in dbContext.CollectionGroups
                    on gu.GroupId equals cg.GroupId into cg_g
                from cg in cg_g.DefaultIfEmpty()
                where !g.AccessAll && cg.CollectionId == cc.CollectionId &&
                (c.Id == _cipherId && 
                (c.UserId == _userId || 
                (!c.UserId.HasValue && ou.Status == OrganizationUserStatusType.Confirmed && o.Enabled &&
                (ou.AccessAll || cu.CollectionId != null || g.AccessAll || cg.CollectionId != null)))) &&
                (c.UserId.HasValue || ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly)
                select c;
            return query;
        }
    }
}
